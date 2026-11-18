#!/usr/bin/env python3
"""
Authors every serialized asset the Lancefall game mode needs (GameModes.Lancefall = 104, hint toast 179 - the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md row 14 reserved for it).

Lancefall is the Shrike's SPACE game: a race to break the plates. The Void Lance is a 2,400 u hitscan that breaks every
block it passes (SHRIKE.md §3), so a lance fired at a plate's FACE breaks a tile, and the same lance laid EDGE-ON along
the plate's plane breaks a whole strip across it. The score is mass taken off the board (ScoringMetric.PrismsDestroyed,
Rampage's rule), so the card reads Space. First DOMAIN to the target wins, golf-timed.

THE ARENA IS SHATTERPLANE'S PLATES, referenced read-only (the Brittle Star's Space game): wide, thin round plates of
neutral prisms, a different arrangement per rung. Why not a fork: a plate is exactly the shape that rewards a hitscan
line (the face is one tile deep, the plane is two dozen wide), and its four rungs already ask the right questions in
the other order - I1's plates face the spawn ring straight on, so the pilot has to swing round to their edge; I2's are
at mixed angles; I3 stacks them 70 u apart, so one plane is one plate; I4's super-shielded ribs stop a lance without
Void Pierce and cut each strip into pieces. The Brittle Star's own crystal field is its controller's, not the cell's,
so it does not come with the plates.

NOTHING ABOUT THE SHRIKE CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the plates, the
crystals (the scene's CrystalManager) and the AI edgers, which only steer.

The scene is cloned from Shatterplane's (its controller declares settings / rule / arenaCell as this one does), with
the identity, the monitor, the AI hulls and the crystals swapped; the cells and the spawn ring are Shatterplane's own.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_lancefall_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import shatterplane_budget as sb  # noqa: E402
import shrike_games_common as common  # noqa: E402

MODE_ID = 104
TOAST_HINT = 179
g = lib.Generator(MODE_ID, "Lancefall")
guid = lib.guid

AR = common.AR
SCRIPT_PATHS = {
    "LancefallController":       f"{AR}/Lancefall/LancefallController.cs",
    "LancefallSettingsSO":       f"{AR}/Lancefall/LancefallSettingsSO.cs",
    "LancefallScoringRuleSO":    f"{AR}/Lancefall/LancefallScoringRuleSO.cs",
    "LancefallEdger":            f"{AR}/Lancefall/LancefallEdger.cs",
    "LancefallPrismTurnMonitor": f"{AR}/TurnMonitors/LancefallPrismTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameLancefall":      guid("asset/ArcadeGameLancefall"),
    "LancefallScoringRule":     guid("asset/LancefallScoringRule"),
    "LancefallSettings":        guid("asset/LancefallSettings"),
    "GameToastConfigLancefall": guid("asset/GameToastConfigLancefall"),
    "ModePreviewLancefall":     guid("asset/ModePreviewLancefall"),
    "MinigameLancefall.unity":  guid("asset/MinigameLancefall.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Shrike"] = common.SHRIKE_VESSEL
SHATTER_DIR = "Assets/_SO_Assets/Cell Configs/Shatterplane Cell"
CELLS = [lib.existing_guid(f"{SHATTER_DIR}/Shatterplane Cell Config {i}.asset") for i in (1, 2, 3, 4)]
for i, gd in enumerate(CELLS, 1):
    EXISTING[f"ShatterplaneCell{i}"] = gd
# The donor scene's identity, minted by author_shatterplane_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameShatterplane.unity"
DONOR = {
    "ShatterplaneController":       guid("script/ShatterplaneController"),
    "ShatterplanePrismTurnMonitor": guid("script/ShatterplanePrismTurnMonitor"),
    "ShatterplaneSettings":         guid("asset/ShatterplaneSettings"),
    "ShatterplaneScoringRule":      guid("asset/ShatterplaneScoringRule"),
}
BRITTLESTAR_ID = lib.VESSEL_CLASS_ID["BrittleStar"]
SHRIKE_ID = common.SHRIKE_ID

# ── The race ─────────────────────────────────────────────────────────────────
METRIC_PRISMS_DESTROYED = 5   # ScoringMetric.PrismsDestroyed
TARGET = 600                  # kept in sync with EndConditionOverridesSO.DefaultLancefallPrismTarget
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate): a quarter of the target behind buys more than one
# element level. Space is the element that widens the lance (radius 1.5 -> 5 u), so it reaches the score.
COMEBACK_RATE = 0.008

SPAWN_RING = int(sb.SPAWN_RING)                       # Shatterplane's ring, kept
CRYSTAL_BALL = 200                                    # Shatterplane's crystal manager's radius, kept
CRYSTALS = [5, 4, 3, 2]                               # each one a missile plant and a step of Space

# The lance, read off the shipped ShrikeConfig: what one shot takes from a plate laid face-on and edge-on.
LANCE_RANGE = common.config_value(g, "lanceRange")
LANCE_COOLDOWN = common.config_value(g, "lanceCooldown")
LANCE_RADIUS_MIN, LANCE_RADIUS_MAX = common.config_range(g, "lanceRadius")
AI_LANCE_MIN_TARGETS = common.config_value(g, "aiLanceMinTargets")
CENSUS = {r["i"]: sb.census(r) for r in sb.RUNGS}


# The AI lays its line CHORD_OFFSET off the plate's centre, across its approach: I4's ribs run through the centre row
# of every plate, so a line through the centre would meet nothing but armour there. Three tiles off still leaves a
# near-full chord on the unribbed rungs.
CHORD_OFFSET = 24.0


def strip(r, radius, offset=CHORD_OFFSET):
    """Breakable tiles of one plate a lance laid in its plane `offset` off its centre takes (a tile is TILE wide, so a
    line within half a tile plus the beam's radius of a row's centre line meets it). Super-shielded ribs stop nothing
    but never break."""
    rad = r["plateRadius"]
    rim_inside, outside = (rad - 1) ** 2, rad * rad
    taken = 0
    for a in range(-rad, rad + 1):
        for b in range(-rad, rad + 1):
            d2 = a * a + b * b
            if d2 > outside or abs(b * sb.TILE - offset) > radius + sb.TILE * 0.5 - 1e-6:
                continue
            rim = d2 > rim_inside
            if not rim and r["ribEvery"] > 0 and (a % r["ribEvery"] == 0 or b % r["ribEvery"] == 0):
                continue   # a super-shielded rib (shatterplane_budget.plate's rule)
            taken += 1
    return taken


STRIPS = {r["i"]: strip(r, LANCE_RADIUS_MIN) for r in sb.RUNGS}

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameLancefall.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
shared_minted = common.emit_shared(g)
g.folder_meta(f"{AR}/Lancefall")
g.text_meta(f"{AR}/LANCEFALL.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/LancefallScoringRule.asset", G_ASSET["LancefallScoringRule"],
             lib.header_for(G_SCRIPT["LancefallScoringRuleSO"], "LancefallScoringRule") +
             f"""  metric: {METRIC_PRISMS_DESTROYED}
  golfRules: 1
  prismsLeftTemplate: '{{count}} Prisms Left'
  breakdownTemplate: '{{count}} Prisms'
  victoryHeader: VICTORY
  victoryLabel: LANCEFALL TIME
  defeatHeader: DEFEAT
  defeatLabel: PRISMS LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds":    ([2.4, 1.8, 1.2, 0.8], "down"),
    "aiStandoff":           ([200, 240, 280, 320], "up"),
    "aiEdgeDegrees":        ([20, 14, 9, 5], "down"),
    "aiAimError":           ([50, 30, 15, 5], "down"),
    "aiCrystalDetourRange": ([150, 220, 300, 400], "up"),
}
SHARED = {"aiChordOffset": CHORD_OFFSET}
g.emit_asset("Assets/_SO_Assets/Games/LancefallSettings.asset", G_ASSET["LancefallSettings"],
             lib.header_for(G_SCRIPT["LancefallSettingsSO"], "LancefallSettings")
             + "".join(common.ladder_yaml(LADDERS, n) for n in LADDERS)
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED.items()))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
DESCRIPTION = ("Shrikes only - break the plates hanging in the cell. Your Void Lance breaks every block on its line, "
               "so a lance at a plate's face takes a tile, but come round to its edge and lay the lance along the plate "
               "and it breaks a whole strip. Crystals widen the beam. Higher intensities turn and stack the plates and "
               "rib them with shields. First team to break the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameLancefall"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameLancefall") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Lancefall
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameLancefall
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Lancefall.asset", G_ASSET["GameToastConfigLancefall"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Lancefall") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(83, "<b>{0}</b> has broken {1} prisms", tint_domain=1, domain_names=0, every_n=100) +
             lib.toast(128, "{0} is a quarter of the way to {2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Come round to a plate's edge and lay your lance along it - it breaks a whole "
                       "strip", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigLancefall"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Lancefall.asset", G_ASSET["ModePreviewLancefall"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Lancefall") + f"""  Mode: {MODE_ID}
  Notes: 'Shatterplane''s plates at each intensity - billboards, the gallery, the shelves and the reliquary. A race
    to a prism target; a solo preview teaches laying the lance along a plate''s edge. Vessel pinned to Shrike({SHRIKE_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{common.cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {SHRIKE_ID}
  ObjectiveText: Lay your lance along a plate's edge
  ObjectiveMetric: {METRIC_PRISMS_DESTROYED}
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RING}
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewLancefall"])

# ── 7. Scene: Shatterplane's, with the identity, the monitor, the AI hulls and the crystals swapped ──
errors = []
SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
         f"  spawnRingRadiusFloor: {SPAWN_RING}\n  spawnFormation: 0\n")
OMNI = common.omni_block(CRYSTAL_BALL, CRYSTALS)
DONOR_OMNI = common.omni_block(200, [0, 0, -1, 1], [2, 1, 1, 0])   # Shatterplane's crystals


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["ShatterplanePrismTurnMonitor"], G_SCRIPT["LancefallPrismTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["ShatterplaneController"], G_SCRIPT["LancefallController"], "controller")
    scene = lib.swap_guid(scene, DONOR["ShatterplaneSettings"], G_ASSET["LancefallSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["ShatterplaneScoringRule"], G_ASSET["LancefallScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {BRITTLESTAR_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {SHRIKE_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_OMNI, OMNI, "crystals")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + common.cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, OMNI]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameLancefall.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameLancefall", G_ASSET["MinigameLancefall.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameLancefall"])
g.register_always_unlocked()
g.register_build_scene("MinigameShatterplane", "MinigameLancefall", G_ASSET["MinigameLancefall.unity"])
g.set_end_condition("lancefallPrismTarget", after="thornbushRoundSeconds", value=TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {TARGET}: a quarter-of-target deficit buys "
                  f"{0.25 * TARGET * COMEBACK_RATE:.2f} levels (< 1)")
for i in (1, 2, 3, 4):
    c = CENSUS[i]
    if 3 * TARGET > c["breakable"]:
        errors.append(f"I{i}: three domains racing to {TARGET} need {3 * TARGET} prisms; the plates hold "
                      f"{c['breakable']} breakable")
    # The game is the edge: one edge-on lance takes at least ten times a face-on lance's one tile.
    if STRIPS[i] < 10:
        errors.append(f"I{i}: an edge-on lance takes only {STRIPS[i]} tiles - laying it along the plate would not matter")
    # And it is a race, not three shots: a perfect pilot needs at least fifteen strips.
    if TARGET / STRIPS[i] < 15:
        errors.append(f"I{i}: {TARGET / STRIPS[i]:.1f} perfect strips reach the target - too few for a race")
    if STRIPS[i] < AI_LANCE_MIN_TARGETS:
        errors.append(f"I{i}: a strip ({STRIPS[i]}) is under the autopilot's lance threshold ({AI_LANCE_MIN_TARGETS:g})")
for r in sb.RUNGS:
    if LANCE_RANGE < 2 * (r["plateRadius"] + 0.5) * sb.TILE + SPAWN_RING:
        errors.append(f"I{r['i']}: the {LANCE_RANGE:g} u lance is shorter than a plate across the arena")

# The load-bearing assumptions: the lance is a hitscan that breaks every block on its line except super-shields without
# Void Pierce, and the autopilot lances at mass that is not its own (the plates are neutral).
executor = g.read(common.SHRIKE_EXECUTOR)
for needle in ("index.QuerySegment(o, o + f * len, r + config.DockQueryReach, _candidates);",
               "if (!ShrikeRules.LanceBreaks(prism.prismProperties is { IsSuperShielded: true }, pierce))",
               "BreakPrism(prism, f * config.LanceDebrisSpeed, devastate: true);",
               "if (prism && !prism.destroyed && prism.Domain != own) count++;"):
    if needle not in executor:
        errors.append(f"ShrikeExecutor no longer has '{needle}' - Lancefall is built on a lance that breaks every block "
                      f"on its line and an autopilot that lances neutral mass; re-check the Void Lance")
if f"LancefallHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.LancefallHint is not {TOAST_HINT} (GAME_KIT_ROSTER.md row 14)")
if f"Lancefall = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Lancefall is not {MODE_ID} (GAME_KIT_ROSTER.md row 14)")
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultLancefallPrismTarget = {TARGET};" not in eco:
    errors.append(f"EndConditionOverridesSO.DefaultLancefallPrismTarget is not {TARGET}")
genre = g.read("Assets/_Scripts/Data/ModeGenre.cs")
if "GameModes.Lancefall" in genre:
    errors.append("ModeGenre has a Lancefall row - the mode reads Space through its metric; delete the row")

errors += common.common_errors(g, SCRIPT_PATHS["LancefallEdger"], "Lancefall")
settings_cs = g.read(SCRIPT_PATHS["LancefallSettingsSO"])
errors += common.ladder_errors(settings_cs, LADDERS, SHARED, "LancefallSettingsSO")
for r in sb.RUNGS:
    if r["ribEvery"] > 0 and round(CHORD_OFFSET / sb.TILE) % r["ribEvery"] == 0:
        errors.append(f"I{r['i']}: the AI's chord offset {CHORD_OFFSET:g} u lies on a rib row")
if "_settings.aiChordOffset" not in g.read(SCRIPT_PATHS["LancefallEdger"]):
    errors.append("LancefallEdger no longer lays its line off the centre row (I4's ribs run through it)")
controller = g.read(SCRIPT_PATHS["LancefallController"])
for banned in ("ShrikeConfig", "SetNucleusWorldRadius", "PerformShipControllerActions", "SetSpawnerPaused"):
    if banned in controller:
        errors.append(f"LancefallController touches {banned} - the mode never changes the vessel or resizes the arena")
if "NucleusIsControlZone = false" not in controller:
    errors.append("LancefallController no longer clears the nucleus's control zone (play geometry, not a claim)")
for i in (1, 2, 3, 4):
    cfg = g.read(f"{SHATTER_DIR}/Shatterplane Cell Config {i}.asset")
    if lib.existing_guid(f"Assets/_Prefabs/Spawnables/SpawnableShatterplane{i}.prefab") not in cfg:
        errors.append(f"Shatterplane Cell Config {i} no longer lays SpawnableShatterplane{i} - re-read the plates")

# The scene.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("LancefallController", "LancefallPrismTurnMonitor")},
                     "LancefallSettings": G_ASSET["LancefallSettings"],
                     "LancefallScoringRule": G_ASSET["LancefallScoringRule"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {SHRIKE_ID}\n") != 4 or f"  - vesselClass: {BRITTLESTAR_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Shrike AI templates")

print(f"edge-on strip {CHORD_OFFSET:g} u off centre, per rung: " + ", ".join(f"I{i} {STRIPS[i]}" for i in (1, 2, 3, 4))
      + f"; {TARGET} prisms is {TARGET / max(STRIPS.values()):.0f}-{TARGET / min(STRIPS.values()):.0f} perfect strips "
      f"({LANCE_COOLDOWN:g} s cooldown)")
g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()) + shared_minted
                + [guid(f"folder/{AR}/Lancefall"), guid(f"text/{AR}/LANCEFALL.md")])
