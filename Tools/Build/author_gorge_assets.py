#!/usr/bin/env python3
"""
Authors every serialized asset the Gorge game mode needs (GameModes.Gorge = 127, hint toast 202 - the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md rows 36-37 reserved for the Jewel Wasp).

Gorge is the Jewel Wasp's SPACE game: a timed feast on a reef. Shoals of neutral (Blue, no-team) prisms hang in the
cell, laid by GorgeController on every peer from the intensity - CHUM'S REEF (ChumReef, pure, referenced: the same
shoals Chum lays). Sting a creature and ride it to a shoal; RT held at prisms is the host's suction, up to 8 of the
nearest rival prisms into its mouth every 0.45 s (JewelWaspConfig suctionBites / suctionInterval). Space widens the
reach (x1 to x3 the creature's own) and Space 5 (Maw) pulls from every side. Every shield is food to a possessed
creature, so the crusted rungs' shells are eaten too. The score is prisms taken off the board (PrismsDestroyed): every
prism the pilot's hull breaks, as every mode counts it, plus every reef prism a creature they ride sucks in (the
suction names its rider). Timed, 90 s.

THE ARENA IS CHUM'S, referenced read-only: its four cells (the Barren nucleus, tameable tadpole herds of 6, 9, 12, 15
grazing the reef, 4-2 omni crystals) and its reef ladder (8 to 16 shoals, crusted on I3-I4, out toward the membrane).
Why not a fork: the wasp needs exactly what Chum authors - creatures to ride and a finite field of prisms for them to
eat, with wild grazers racing you for it.

NOTHING ABOUT THE WASP CHANGES (the LOCKED vessel rule, Shombith 2026-10-31). The mode's levers are the reef, the herds,
the crystals and the AI gluttons, which only steer the FREE wasp (wasp_games_common.AUTOPILOT_NEEDLES).

The scene is cloned from Chum's (its controller declares settings / rule / arenaCell / reefPrefab / nucleusRadius /
membraneRadius exactly as this one does), with the identity, the monitor and the AI hulls swapped.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_gorge_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import wasp_games_common as common  # noqa: E402

MODE_ID = 127
TOAST_HINT = 202
TOAST_LEAD = 130             # DomainRaceLeadChanged, posted by the controller's own lead sampler
ROUND_SECONDS = 90           # Docs/ARCADE_MATCH_LENGTH.md; EndConditionOverridesSO.DefaultGorgeRoundSeconds
g = lib.Generator(MODE_ID, "Gorge")
guid = lib.guid

AR = common.AR
SCRIPT_PATHS = {
    "GorgeController":      f"{AR}/Gorge/GorgeController.cs",
    "GorgeSettingsSO":      f"{AR}/Gorge/GorgeSettingsSO.cs",
    "GorgeScoringRuleSO":   f"{AR}/Gorge/GorgeScoringRuleSO.cs",
    "GorgeGlutton":         f"{AR}/Gorge/GorgeGlutton.cs",
    "GorgeTimeTurnMonitor": f"{AR}/TurnMonitors/GorgeTimeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameGorge":      guid("asset/ArcadeGameGorge"),
    "GorgeScoringRule":     guid("asset/GorgeScoringRule"),
    "GorgeSettings":        guid("asset/GorgeSettings"),
    "GameToastConfigGorge": guid("asset/GameToastConfigGorge"),
    "ModePreviewGorge":     guid("asset/ModePreviewGorge"),
    "MinigameGorge.unity":  guid("asset/MinigameGorge.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_JewelWasp"] = common.WASP_VESSEL
CHUM_REEF = f"{AR}/Chum/ChumReef.cs"
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Chum Cell"
CELLS = [guid(f"asset/ChumCellConfig{i}") for i in (1, 2, 3, 4)]   # Chum's own, referenced read-only
for i, gd in enumerate(CELLS, 1):
    EXISTING[f"ChumCell{i}"] = gd
SPAWNABLE_PRISM_PATH = "Assets/_Prefabs/Trails/SpawnablePrism.prefab"
EXISTING["SpawnablePrism"] = lib.existing_guid(SPAWNABLE_PRISM_PATH)
SPAWNABLE_PRISM_FILEID = 4563009547826722997
FAUNA_POSSESSION_CS = "Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.Possession.cs"
PRISM_CS = "Assets/_Scripts/Controller/Vessel/Prism.cs"
# The donor scene's identity, minted by author_chum_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameChum.unity"
DONOR = {
    "ChumController":      guid("script/ChumController"),
    "ChumTimeTurnMonitor": guid("script/ChumTimeTurnMonitor"),
    "ChumSettings":        guid("asset/ChumSettings"),
    "ChumScoringRule":     guid("asset/ChumScoringRule"),
}
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]
WASP_ID = common.WASP_ID

NUCLEUS_RADIUS = 392         # Chum's controller fields, kept
MEMBRANE_RADIUS = 1200
SPAWN_RING = 450             # Chum's ring
# The reef (ChumReef.ForIntensity), checked against the C# below: (shoals, shells, band, crusted).
RUNGS = [(8, 2, 0.30, False), (12, 2, 0.45, False), (12, 3, 0.45, True), (16, 3, 0.60, True)]
REEF_PRISMS_I2 = 768         # author_chum_assets.py's own count of the I2 reef

# Comeback - a FUNCTION OF THE REFERENCE MATCH (timed: the "target" is a reference domain total): a domain eating a
# quarter of the I2 reef in the round. A quarter of that behind buys >= 1 element level; on the wasp, Space is the
# suction's reach, so the comeback reaches the meal.
REFERENCE_DOMAIN_PRISMS = REEF_PRISMS_I2 // 4
COMEBACK_RATE = 0.025

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameGorge.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
shared_minted = common.emit_shared(g)
g.folder_meta(f"{AR}/Gorge")
g.text_meta(f"{AR}/GORGE.md")

# ── 2. Scoring rule (timed, highest wins, PrismsDestroyed) ───────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/GorgeScoringRule.asset", G_ASSET["GorgeScoringRule"],
             lib.header_for(G_SCRIPT["GorgeScoringRuleSO"], "GorgeScoringRule") +
             """  metric: 5
  golfRules: 0
  prismsTemplate: '{count} Prisms'
  eatenTemplate: '{count} sucked in'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: PRISMS GORGED
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
SHARED_1 = {"leadAnnounceAfterSeconds": 15, "leadAnnounceMinGapSeconds": 8, "leadSampleSeconds": 1}
LADDERS = {
    "aiRetargetSeconds": ([6, 4.5, 3.5, 2.5], "down"),
    "aiHostChoosiness":  ([0.25, 0.5, 0.75, 0.95], "up"),
    "aiAimError":        ([60, 35, 15, 5], "down"),
    "aiFullShoalWeight": ([0, 0.3, 0.6, 0.9], "up"),
}

g.emit_asset("Assets/_SO_Assets/Games/GorgeSettings.asset", G_ASSET["GorgeSettings"],
             lib.header_for(G_SCRIPT["GorgeSettingsSO"], "GorgeSettings")
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED_1.items())
             + "".join(common.ladder_yaml(LADDERS, n) for n in LADDERS))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 1: a feast against the arena - the wild grazers eat the reef from a lone wasp too.
DESCRIPTION = ("Jewel Wasps only - a timed feast on a reef. Shoals of prisms hang round the core. Sting a creature with "
               "a pull of RT, ride it to a shoal and hold RT: it sucks the prisms in. Space widens the pull, and at "
               "Space 5 it pulls from every side; every shield is food to a creature you ride. An omni crystal hatches "
               "a predator you ride at once. The most prisms taken off the board when the clock runs out wins. Higher "
               "intensities put the shoals farther out under a shielded crust, with more wild grazers racing you.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameGorge"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameGorge") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Gorge
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameGorge
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_JewelWasp']}, type: 2}}
  MinPlayersAllowed: 1
  MaxPlayersAllowed: 9
  MinDomainsAllowed: 1
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Gorge.asset", G_ASSET["GameToastConfigGorge"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Gorge") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(TOAST_LEAD, "{0} takes the lead - {1} prisms gorged", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Pull RT to sting a creature and ride it to a shoal. Hold RT to suck the prisms in",
                       domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigGorge"])

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Gorge.asset", G_ASSET["ModePreviewGorge"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Gorge") + f"""  Mode: {MODE_ID}
  Notes: 'Chum''s reef cell at each intensity. OPEN-ENDED - the reef is laid by GorgeController at match start,
    so a preview has none - it teaches the sting, the ride and the herds to choose from. Vessel pinned to
    JewelWasp({WASP_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{common.cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {WASP_ID}
  ObjectiveText: Sting a creature, ride it to a shoal and hold RT to suck the prisms in
  ObjectiveMetric: 5
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RING}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {SPAWN_RING}\n" for _ in RUNGS) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewGorge"])

# ── 7. Scene: Chum's, with the identity, the monitor and the AI hulls swapped ─
errors = []


def monitor(script):
    return (f"  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n"
            "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
            f"  duration: {ROUND_SECONDS}\n")


def controller_tail(settings, rule):
    return (f"  settings: {{fileID: 11400000, guid: {settings}, type: 2}}\n"
            f"  rule: {{fileID: 11400000, guid: {rule}, type: 2}}\n"
            "  arenaCell: {fileID: 1700000065}\n"
            f"  reefPrefab: {{fileID: {SPAWNABLE_PRISM_FILEID}, guid: {EXISTING['SpawnablePrism']}, type: 3}}\n"
            f"  nucleusRadius: {NUCLEUS_RADIUS}\n"
            f"  membraneRadius: {MEMBRANE_RADIUS}\n")


MONITOR_DONOR = monitor(DONOR["ChumTimeTurnMonitor"])
MONITOR = monitor(G_SCRIPT["GorgeTimeTurnMonitor"])
CONTROLLER_DONOR_TAIL = controller_tail(DONOR["ChumSettings"], DONOR["ChumScoringRule"])
CONTROLLER_TAIL = controller_tail(G_ASSET["GorgeSettings"], G_ASSET["GorgeScoringRule"])


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.replace_block(scene, MONITOR_DONOR, MONITOR, "turn monitor")
    scene = lib.replace_block(scene, CONTROLLER_DONOR_TAIL, CONTROLLER_TAIL, "controller fields")
    scene = lib.swap_guid(scene, DONOR["ChumController"], G_SCRIPT["GorgeController"], "controller")
    scene = lib.swap_guid(scene, f"  - vesselClass: {ANGLER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {WASP_ID}\n    PlayerName: AI", "AI templates", expected=4)
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + common.cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", MONITOR,
                   CONTROLLER_TAIL]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameGorge.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameGorge", G_ASSET["MinigameGorge.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameGorge"])
g.register_always_unlocked()
g.register_build_scene("MinigameChum", "MinigameGorge", G_ASSET["MinigameGorge.unity"])
g.set_end_condition("gorgeRoundSeconds", after="marionetteStrikeTarget", value=ROUND_SECONDS)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * REFERENCE_DOMAIN_PRISMS * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead: a quarter of the reference domain ({REFERENCE_DOMAIN_PRISMS} "
                  f"prisms) behind buys {0.25 * REFERENCE_DOMAIN_PRISMS * COMEBACK_RATE:.2f} element levels (< 1)")

# The reef is Chum's: the ladder Gorge describes is the one ChumReef lays.
reef_cs = g.read(CHUM_REEF)
for i, (shoals, shells, band, crusted) in enumerate(RUNGS, 1):
    row = f"{i if i < 4 else '_'} => new Rung({shoals}, {shells}, {band:.2f}f, {'true' if crusted else 'false'}),"
    if row not in reef_cs:
        errors.append(f"ChumReef.ForIntensity no longer has '{row}' - Gorge's description of the reef is stale")

# The load-bearing assumptions: a possessed creature's suction eats rival (Blue included) prisms as its rider, every
# shield is food, and the eaten prism remembers who ate it.
executor = g.read(common.WASP_EXECUTOR)
if "int bites = host.PossessedSuction(config.SuctionReachScale.EvaluateLive(_status), config.SuctionBites, SpaceUpgraded);" \
        not in executor:
    errors.append("JewelWaspExecutor's RT in a host is no longer the creature's suction - Gorge has nothing to score")
poss = g.read(FAUNA_POSSESSION_CS)
for needle in ("if (prism.Domain == rider.PossessorDomain) return false;",
               "BiteMass(prism, mouth, rider.PossessorDomain, rider.PossessorName, true)"):
    if needle not in poss:
        errors.append(f"Fauna.Possession no longer has '{needle}' - a ridden creature must eat the neutral reef in its "
                      f"rider's name, shields and all")
prism_cs = g.read(PRISM_CS)
for needle in ("public bool DestroyedByCreature => _destroyedByCreature;", "public string DestroyerName { get; private set; }"):
    if needle not in prism_cs:
        errors.append(f"Prism no longer has '{needle}' - GorgeController tells a ridden creature's meal by it")
controller = g.read(SCRIPT_PATHS["GorgeController"])
if "if (!SimulatedHere(pilot)) stats.HostilePrismsDestroyed += 1;" not in controller \
        or re.search(r"HostilePrismsDestroyed = (?!0;|prisms\[i\];)", controller):
    errors.append("GorgeController must add only a remote pilot's meal to HostilePrismsDestroyed and never assign it "
                  "mid-match (StatsManager owns every other count)")
if "R_VesselActions" in controller:
    errors.append("GorgeController reaches into the vessel - a mode never changes the vessel (LOCKED)")

# Enum ids, the default round and the provider.
toasts_cs = g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs")
if f"GorgeHint = {TOAST_HINT}," not in toasts_cs:
    errors.append(f"GameToastSituation.GorgeHint is not {TOAST_HINT} (GAME_KIT_ROSTER.md rows 36-37)")
if f"DomainRaceLeadChanged = {TOAST_LEAD}," not in toasts_cs:
    errors.append(f"GameToastSituation.DomainRaceLeadChanged is not {TOAST_LEAD}")
if f"Gorge = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Gorge is not {MODE_ID} (GAME_KIT_ROSTER.md rows 36-37)")
if f"DefaultGorgeRoundSeconds = {ROUND_SECONDS};" not in g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"):
    errors.append("EndConditionOverridesSO.DefaultGorgeRoundSeconds differs from ROUND_SECONDS")
if "case GameModes.Gorge:" not in g.read("Assets/_Scripts/UI/MiniGameHUD.cs"):
    errors.append("MiniGameHUD has no objective provider case for Gorge")

errors += common.common_errors(g, SCRIPT_PATHS["GorgeGlutton"], "Gorge")
errors += common.ladder_errors(g.read(SCRIPT_PATHS["GorgeSettingsSO"]), LADDERS, SHARED_1, "GorgeSettingsSO")

# The arena: every rung has creatures to ride.
for i in (1, 2, 3, 4):
    if "  SpawnProfile: {fileID: 0}" in g.read(f"{CELL_DIR}/Chum Cell Config {i}.asset"):
        errors.append(f"Chum Cell Config {i} spawns nothing - the wasp would have no creature to ride")

# The scene: every new identity present, every donor one gone, four wasp AIs, the timed monitor.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("GorgeController", "GorgeTimeTurnMonitor")},
                     "GorgeSettings": G_ASSET["GorgeSettings"], "GorgeScoringRule": G_ASSET["GorgeScoringRule"],
                     "SpawnablePrism": EXISTING["SpawnablePrism"],
                     **{f"cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {WASP_ID}\n") != 4 or f"  - vesselClass: {ANGLER_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Jewel Wasp AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()) + shared_minted
                + [guid(f"folder/{AR}/Gorge"), guid(f"text/{AR}/GORGE.md")])
