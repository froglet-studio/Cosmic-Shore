#!/usr/bin/env python3
"""
Authors every serialized asset the Thornbush game mode needs (GameModes.Thornbush = 103, hint toast 178 - the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md row 13 reserved for it).

Thornbush is the Shrike's MASS game: a timed nest-building match in a heap of sticks. Dock on a stick, press Mass, and
the Shrike leaves a NEST - a shell of super-shielded plates (12 at Mass 0, 32 at Mass 10) round hoops of shielded
twigs and a lining of plain pads, crowned with a missile plant
(SHRIKE.md §3). The score is the mass standing in your colour at the whistle (ScoringMetric.VolumeRemaining, a live
stock): every nest block and trail block is laid in the pilot's name, so StatsManager already counts it in and out.
Rival missiles crack the shields and break what is under them; with Void Pierce (Space 5) a rival's lance breaks nest
blocks outright. The Shrike keeps at most three nests, so the match is where to build, when to rebuild, and whose
nests to crack.

THE ARENA IS JACKSTRAW'S HEAP, referenced read-only (the Thresher's Charge game): sticks and jacks round clearings, red
tips and ironwood hearts at the higher rungs. Why not a fork: a nest needs a perch, and a heap of sticks IS a thorn
bush - every stick is one, and the clearings are where the crystals (missile plants) float. Its ladder grows the heap
(360 -> 680 u, 408 -> 3,566 prisms) and adds danger tips.

NOTHING ABOUT THE SHRIKE CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the heap, the
crystals per intensity (the scene's CrystalManager) and the AI builders, which only steer.

The scene is cloned from Glasshouse's (the newest timed VolumeRemaining game, whose controller declares settings / rule /
arenaCell as this one does), with the identity, the monitor, the cells, the AI hulls, the spawn ring and the crystals
swapped.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_thornbush_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import shrike_games_common as common  # noqa: E402

MODE_ID = 103
TOAST_HINT = 178
g = lib.Generator(MODE_ID, "Thornbush")
guid = lib.guid

AR = common.AR
SCRIPT_PATHS = {
    "ThornbushController":       f"{AR}/Thornbush/ThornbushController.cs",
    "ThornbushSettingsSO":       f"{AR}/Thornbush/ThornbushSettingsSO.cs",
    "ThornbushScoringRuleSO":    f"{AR}/Thornbush/ThornbushScoringRuleSO.cs",
    "ThornbushBuilder":          f"{AR}/Thornbush/ThornbushBuilder.cs",
    "ThornbushTimeTurnMonitor":  f"{AR}/TurnMonitors/ThornbushTimeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameThornbush":      guid("asset/ArcadeGameThornbush"),
    "ThornbushScoringRule":     guid("asset/ThornbushScoringRule"),
    "ThornbushSettings":        guid("asset/ThornbushSettings"),
    "GameToastConfigThornbush": guid("asset/GameToastConfigThornbush"),
    "ModePreviewThornbush":     guid("asset/ModePreviewThornbush"),
    "MinigameThornbush.unity":  guid("asset/MinigameThornbush.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Shrike"] = common.SHRIKE_VESSEL
JACKSTRAW_DIR = "Assets/_SO_Assets/Cell Configs/Jackstraw Cell"
CELLS = [lib.existing_guid(f"{JACKSTRAW_DIR}/Jackstraw Cell Config {i}.asset") for i in (1, 2, 3, 4)]
for i, gd in enumerate(CELLS, 1):
    EXISTING[f"JackstrawCell{i}"] = gd
# The heap's radius per rung, read off each rung's own environment prefab (the MAP the AI picks perches in).
HEAP_RADII = []
for i in (1, 2, 3, 4):
    prefab = g.read(f"Assets/_Prefabs/Spawnables/SpawnableJackstraw{i}.prefab")
    m = re.search(r"^  heapRadius: ([0-9.]+)$", prefab, re.M)
    assert m, f"SpawnableJackstraw{i}.prefab has no heapRadius"
    HEAP_RADII.append(float(m.group(1)))
    env_guid = lib.existing_guid(f"Assets/_Prefabs/Spawnables/SpawnableJackstraw{i}.prefab")
    assert env_guid in g.read(f"{JACKSTRAW_DIR}/Jackstraw Cell Config {i}.asset"), \
        f"Jackstraw Cell Config {i} no longer lays SpawnableJackstraw{i} - re-read the heap radius from its environment"
# The donor scene's identity, minted by author_glasshouse_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameGlasshouse.unity"
DONOR = {
    "GlasshouseController":      guid("script/GlasshouseController"),
    "GlasshouseTimeTurnMonitor": guid("script/GlasshouseTimeTurnMonitor"),
    "GlasshouseSettings":        guid("asset/GlasshouseSettings"),
    "GlasshouseScoringRule":     guid("asset/GlasshouseScoringRule"),
}
DONOR_CELLS = [guid(f"asset/GlasshouseCellConfig{i}") for i in (1, 2, 3, 4)]
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]
SHRIKE_ID = common.SHRIKE_ID

# ── The round ────────────────────────────────────────────────────────────────
ROUND_SECONDS = 90        # the arcade's target length; kept in sync with EndConditionOverridesSO.DefaultThornbushRoundSeconds

# The reference domain - ONE Shrike's whole round, read off the shipped ShrikeConfig: three nests of the Mass-0 block
# count, plus the trail it lays cruising for the whole round (one thin block per Mass-0 wavelength).
NEST_BLOCK = common.config_value(g, "nestBlockSize")
MAX_NESTS = int(common.config_value(g, "maxNests"))
NEST_BLOCKS_MIN, NEST_BLOCKS_MAX = common.config_range(g, "nestBlocks")
WAVELENGTH_MIN, _ = common.config_range(g, "trailWavelength")
TRAIL_BLOCK = common.cruise_trail_block(g)
NEST_VOLUME = common.nest_volume(g, int(NEST_BLOCKS_MIN))   # shell plates + weave + lining (ShrikeRules.NestPieces)
TRAIL_VOLUME = common.CRUISE_SPEED * ROUND_SECONDS / WAVELENGTH_MIN * TRAIL_BLOCK
REFERENCE_DOMAIN_VOLUME = MAX_NESTS * NEST_VOLUME + TRAIL_VOLUME
# Comeback - a FUNCTION OF THE SCORE'S SCALE (bonusLevels = deficit x rate): a quarter of the reference domain behind
# buys at least one element level. Mass is the element that grows a nest (12 -> 32 blocks), so it reaches the score.
COMEBACK_RATE = round(1.1 / (0.25 * REFERENCE_DOMAIN_VOLUME), 5)   # a quarter behind buys 1.1 levels, whatever the scale

SPAWN_RINGS = [450, 560, 660, 770]                    # Jackstraw's ring round the same heap
CRYSTAL_BALL = 250                                    # among the sticks, inside the smallest heap (360 u)
CRYSTALS = [5, 4, 3, 2]                               # every one is a missile plant to crack nests with

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameThornbush.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
shared_minted = common.emit_shared(g)
g.folder_meta(f"{AR}/Thornbush")
g.text_meta(f"{AR}/THORNBUSH.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/ThornbushScoringRule.asset", G_ASSET["ThornbushScoringRule"],
             lib.header_for(G_SCRIPT["ThornbushScoringRuleSO"], "ThornbushScoringRule") + """  metric: 12
  golfRules: 0
  volumeTemplate: '{count} Volume'
  blocksTemplate: '{count} blocks'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: VOLUME STANDING
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiPerchSeconds": ([9, 7, 5.5, 4], "down"),
    "aiRaidChance":   ([0, 0.2, 0.35, 0.5], "up"),
    "aiAimError":     ([40, 25, 12, 4], "down"),
    "aiRaidSeconds":  ([3, 4, 5, 6], "up"),
}
LEAD = {"leadSampleSeconds": 1, "leadAnnounceAfterSeconds": 15, "leadAnnounceMinGapSeconds": 8}
SHARED = {"aiPerchSpacing": 120, "aiScanSeconds": 1, "aiScanMargin": 200}

g.emit_asset("Assets/_SO_Assets/Games/ThornbushSettings.asset", G_ASSET["ThornbushSettings"],
             lib.header_for(G_SCRIPT["ThornbushSettingsSO"], "ThornbushSettings")
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in LEAD.items())
             + "  heapRadiusByIntensity:\n" + "".join(f"  - {lib.num(r)}\n" for r in HEAP_RADII)
             + "".join(common.ladder_yaml(LADDERS, n) for n in LADDERS)
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED.items()))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
DESCRIPTION = ("Shrikes only - build in the thorn bush. Dock on a stick and press Mass to leave a nest - a shell of "
               "super-shielded plates round a weave of twigs, crowned with a missile plant. You keep three; a fourth retires the oldest. The "
               "score is the mass standing in your colour when the clock runs out, nests and trail. Paint a rival to "
               "send your missiles at their nests and break what is under the shields. Higher intensities grow the "
               "heap and tip its sticks with danger. Most mass standing at the whistle wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameThornbush"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameThornbush") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Thornbush
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameThornbush
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Shrike']}, type: 2}}
  MinPlayersAllowed: 1
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Thornbush.asset", G_ASSET["GameToastConfigThornbush"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Thornbush") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(130, "{0} takes the lead - {1} volume standing", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Dock on a stick and press Mass to leave a nest. Hold the most mass when the clock "
                       "runs out", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigThornbush"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Thornbush.asset", G_ASSET["ModePreviewThornbush"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Thornbush") + f"""  Mode: {MODE_ID}
  Notes: 'Jackstraw''s heap at each intensity - more sticks, then danger tips. OPEN-ENDED - it scores on mass
    standing in your colour, and what a solo preview teaches is the loop - dock on a stick, press Mass to leave
    a nest, move on. Vessel pinned to Shrike({SHRIKE_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{common.cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {SHRIKE_ID}
  ObjectiveText: Dock on a stick and press Mass to leave a nest
  ObjectiveMetric: 12
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RINGS[0]}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {v}\n" for v in SPAWN_RINGS) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewThornbush"])

# ── 7. Scene: Glasshouse's, with the identity, the monitor, the cells, the AI hulls, the ring and the crystals swapped ──
errors = []
SPAWN = common.spawn_block(SPAWN_RINGS)
DONOR_SPAWN = common.spawn_block([450, 450, 450, 450])     # Glasshouse's ring
OMNI = common.omni_block(CRYSTAL_BALL, CRYSTALS)
DONOR_OMNI = common.omni_block(200, [4, 3, 3, 2])          # Glasshouse's crystals
MONITOR_TAIL = "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
MONITOR_DONOR = (f"  m_Script: {{fileID: 11500000, guid: {DONOR['GlasshouseTimeTurnMonitor']}, type: 3}}\n"
                 + MONITOR_TAIL + "  duration: 90\n")
MONITOR = (f"  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['ThornbushTimeTurnMonitor']}, type: 3}}\n"
           + MONITOR_TAIL + f"  duration: {ROUND_SECONDS}\n")


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.replace_block(scene, MONITOR_DONOR, MONITOR, "turn monitor")
    scene = lib.swap_guid(scene, DONOR["GlasshouseController"], G_SCRIPT["ThornbushController"], "controller")
    scene = lib.swap_guid(scene, DONOR["GlasshouseSettings"], G_ASSET["ThornbushSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["GlasshouseScoringRule"], G_ASSET["ThornbushScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + common.cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + common.cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {ANGLER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {SHRIKE_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, DONOR_OMNI, OMNI, "crystals")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + common.cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, OMNI, MONITOR]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameThornbush.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameThornbush", G_ASSET["MinigameThornbush.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameThornbush"])
g.register_always_unlocked()
g.register_build_scene("MinigameGlasshouse", "MinigameThornbush", G_ASSET["MinigameThornbush.unity"])
g.set_end_condition("thornbushRoundSeconds", after="larderMissileTarget", value=ROUND_SECONDS)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead: a quarter of the reference domain volume "
                  f"({REFERENCE_DOMAIN_VOLUME:,.0f}) behind buys {0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE:.2f} "
                  f"element levels (< 1)")
if NEST_VOLUME < 4 * TRAIL_VOLUME / MAX_NESTS:
    errors.append(f"one nest ({NEST_VOLUME:,.0f}) is worth less than four times a nest's share of the whole round's trail "
                  f"({TRAIL_VOLUME:,.0f}) - the game would be a trail game, not a nest game")

# The load-bearing assumptions: a nest is laid in the pilot's NAME (so StatsManager counts it), its shell super-shielded
# (ShrikeRules.NestPieces: the shell, the weave, the lining), capped,
# and the lance only breaks super-shields with Void Pierce.
executor = g.read(common.SHRIKE_EXECUTOR)
for needle in ("var prism = BoostRingBuilder.LayOne(channel, centre + rot * piece.Local, face, piece.Scale, piece.Kind,\n"
               "                    _status.Domain, _status.PlayerName, _status.PlayerName);",
               "if (live >= config.MaxNests && oldest != null) RetireNest(oldest);",
               "if (!ShrikeRules.LanceBreaks(prism.prismProperties is { IsSuperShielded: true }, pierce))",
               "public bool Docked =>"):
    if needle not in executor:
        errors.append(f"ShrikeExecutor no longer has '{needle}' - Thornbush scores nest blocks laid in the pilot's name "
                      f"through StatsManager; re-check how the Shrike lays a nest")
stats = g.read("Assets/_Scripts/Controller/Managers/StatsManager.cs")
if "roundStats.VolumeRemaining += prismStats.Volume;" not in stats:
    errors.append("StatsManager no longer credits a pilot's VolumeRemaining when their prism is created")
if f"ThornbushHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.ThornbushHint is not {TOAST_HINT} (GAME_KIT_ROSTER.md row 13)")
if f"Thornbush = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Thornbush is not {MODE_ID} (GAME_KIT_ROSTER.md row 13)")
if f"DefaultThornbushRoundSeconds = {ROUND_SECONDS};" not in g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"):
    errors.append("EndConditionOverridesSO.DefaultThornbushRoundSeconds differs from ROUND_SECONDS")

errors += common.common_errors(g, SCRIPT_PATHS["ThornbushBuilder"], "Thornbush")
settings_cs = g.read(SCRIPT_PATHS["ThornbushSettingsSO"])
errors += common.ladder_errors(settings_cs, LADDERS, {**LEAD, **SHARED}, "ThornbushSettingsSO")
radii_init = "{ " + ", ".join(lib.num(r) + "f" for r in HEAP_RADII) + " }"
if f"heapRadiusByIntensity = {radii_init};" not in settings_cs:
    errors.append(f"ThornbushSettingsSO.heapRadiusByIntensity initializer differs from the heaps' {radii_init}")
if "SelectedIntensity" not in g.read(SCRIPT_PATHS["ThornbushController"]):
    errors.append("ThornbushController no longer reads the selected intensity for the heap's radius")

# The arena: crystals among the sticks, pilots outside the heap.
for i, (ring, heap) in enumerate(zip(SPAWN_RINGS, HEAP_RADII), 1):
    if not (heap + 60 <= ring <= lib.MEMBRANE_RADIUS - 300):
        errors.append(f"I{i}: spawn ring {ring} must sit just outside the {heap:.0f} u heap and inside the membrane")
if CRYSTAL_BALL >= min(HEAP_RADII):
    errors.append(f"the crystals' {CRYSTAL_BALL} u ball reaches out of the smallest heap")
if any(b > a for a, b in zip(CRYSTALS, CRYSTALS[1:])) or min(CRYSTALS) < 1:
    errors.append(f"the crystal ladder must run down with intensity and never reach 0 ({CRYSTALS})")
for i in (1, 2, 3, 4):
    if "  NucleusPrefab: {fileID: 0}" not in g.read(f"{JACKSTRAW_DIR}/Jackstraw Cell Config {i}.asset"):
        errors.append(f"Jackstraw Cell Config {i} grew a nucleus - the crystals would move into it")

# The scene.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("ThornbushController", "ThornbushTimeTurnMonitor")},
                     "ThornbushSettings": G_ASSET["ThornbushSettings"],
                     "ThornbushScoringRule": G_ASSET["ThornbushScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in {**DONOR, **{f"Glasshouse cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}}.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {SHRIKE_ID}\n") != 4 or f"  - vesselClass: {ANGLER_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Shrike AI templates")

print(f"reference domain: {MAX_NESTS} nests x {NEST_VOLUME:,.0f} + trail {TRAIL_VOLUME:,.0f} = "
      f"{REFERENCE_DOMAIN_VOLUME:,.0f}; a quarter behind buys {0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE:.2f} levels")
g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()) + shared_minted
                + [guid(f"folder/{AR}/Thornbush"), guid(f"text/{AR}/THORNBUSH.md")])
