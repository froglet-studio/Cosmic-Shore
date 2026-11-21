#!/usr/bin/env python3
"""
Authors every serialized asset the Cave-in game mode needs (GameModes.CaveIn = 107).

Cave-in is the Meerkat's SPACE game (Docs/ElementalLoop/GAME_KIT_ROSTER.md row 17, which reserved id 107 and hint toast
182): "the tunnel sucks in prisms and fires them out to smash others". The Meerkat breaks nothing by flying into
mass. A soliton formed beside mass SWALLOWS the prisms around it (MeerkatSwallow) and fires them out of its way out,
and every fired prism that runs into another destroys both, credited to the soliton's maker (PrismMotion.Annihilate).
So the score is mass taken off the board (ScoringMetric.PrismsDestroyed, Rampage's rule) and the card reads Space.
First DOMAIN to the target wins, golf-timed.

THE ARENA IS SHATTERPLANE'S, referenced read-only: its four cell configs, one plate layout per intensity, every prism
neutral (author_shatterplane_assets.py and shatterplane_budget.py own them):
  I1 Billboards - ten plates facing out.            Tunnel into a plate face on.
  I2 Gallery    - twelve plates at mixed angles.    Line two up so what one tunnel fires out caves in the next.
  I3 Shelves    - stacks of three parallel plates.  One tunnel, three plates.
  I4 Reliquary  - ribbed plates behind grilles.     The super-shielded ribs and screens never break.
This controller does NOT resize the nucleus or lay Shatterplane's crystal field (both are the Brittle Star ring's
levers); the omni crystals are the scene's CrystalManager's, kept from the donor.

NOTHING ABOUT THE MEERKAT CHANGES (the LOCKED vessel rule). The scene is cloned from Shatterplane's, whose controller
declares settings / rule / arenaCell as this one does; only the identity and the AI hulls are swapped.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_cavein_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import meerkat_games_common as mk  # noqa: E402
import shatterplane_budget as sb  # noqa: E402

MODE_ID = 107
TOAST_HINT = 182
g = lib.Generator(MODE_ID, "CaveIn")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "CaveInController":        f"{AR}/CaveIn/CaveInController.cs",
    "CaveInSettingsSO":        f"{AR}/CaveIn/CaveInSettingsSO.cs",
    "CaveInScoringRuleSO":     f"{AR}/CaveIn/CaveInScoringRuleSO.cs",
    "CaveInPrismTurnMonitor":  f"{AR}/TurnMonitors/CaveInPrismTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameCaveIn":      guid("asset/ArcadeGameCaveIn"),
    "CaveInScoringRule":     guid("asset/CaveInScoringRule"),
    "CaveInSettings":        guid("asset/CaveInSettings"),
    "GameToastConfigCaveIn": guid("asset/GameToastConfigCaveIn"),
    "ModePreviewCaveIn":     guid("asset/ModePreviewCaveIn"),
    "MinigameCaveIn.unity":  guid("asset/MinigameCaveIn.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Meerkat"] = lib.VESSELS["Meerkat"]
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Shatterplane Cell"
CELLS = []
for i in (1, 2, 3, 4):
    gd = lib.existing_guid(f"{CELL_DIR}/Shatterplane Cell Config {i}.asset")
    EXISTING[f"ShatterplaneCellConfig{i}"] = gd
    CELLS.append(gd)
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameShatterplane.unity"
DONOR = {
    "ShatterplaneController":       guid("script/ShatterplaneController"),
    "ShatterplanePrismTurnMonitor": guid("script/ShatterplanePrismTurnMonitor"),
    "ShatterplaneSettings":         guid("asset/ShatterplaneSettings"),
    "ShatterplaneScoringRule":      guid("asset/ShatterplaneScoringRule"),
}
BRITTLESTAR_ID = lib.VESSEL_CLASS_ID["BrittleStar"]
MEERKAT_ID = lib.VESSEL_CLASS_ID["Meerkat"]
SPAWNABLE = "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableShatterplane.cs"
PLATES = "Assets/_Scripts/Controller/Environment/MiniGameObjects/ShatterplanePlates.cs"
PRISM_MOTION = "Assets/_Scripts/Controller/Prisms/PrismMotion.cs"

# ── The race ─────────────────────────────────────────────────────────────────
METRIC_PRISMS_DESTROYED = 5   # ScoringMetric.PrismsDestroyed
# One target at every rung: about TARGET_SHARE of the SMALLEST rung's breakable plate mass, rounded to 100 (the
# Reliquary's ribs and screens never break, so it has the least). An ESTIMATE until a timed match: a soliton fires a
# few dozen prisms and each that strikes another breaks two, so a domain of two pilots mining steadily should reach it in
# about two minutes. Kept in sync with EndConditionOverridesSO.DefaultCaveInPrismTarget (asserted below).
TARGET_SHARE = 0.30
CENSUS = {r["i"]: sb.census(r) for r in sb.RUNGS}
TARGET = int(round(TARGET_SHARE * min(c["breakable"] for c in CENSUS.values()) / 100.0)) * 100
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate): a quarter of the target behind buys more than one
# element level.
COMEBACK_RATE = 0.005

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameCaveIn.asset"

# ── 1. .cs.meta, folders, doc ────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/CaveIn")
g.text_meta(f"{AR}/CAVEIN.md")
mk.shared_metas(g)

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/CaveInScoringRule.asset", G_ASSET["CaveInScoringRule"],
             lib.header_for(G_SCRIPT["CaveInScoringRuleSO"], "CaveInScoringRule") +
             f"""  metric: {METRIC_PRISMS_DESTROYED}
  golfRules: 1
  prismsLeftTemplate: '{{count}} Prisms Left'
  breakdownTemplate: '{{count}} Prisms'
  victoryHeader: VICTORY
  victoryLabel: CAVE-IN TIME
  defeatHeader: DEFEAT
  defeatLabel: PRISMS LEFT
""")

# ── 3. Settings (the ladders: index = AI difficulty - 1) ─────────────────────
LADDERS = {
    "aiRetargetSeconds":   ([3, 2.4, 1.8, 1.3], "down"),
    "aiChoicesPerPlate":   ([2, 3, 4, 5], "up"),
    "aiLineUpChance":      ([0.2, 0.4, 0.6, 0.8], "up"),
    "aiRaidChance":        ([0.05, 0.1, 0.15, 0.2], "up"),
    "aiRaidRange":         ([250, 350, 450, 550], "up"),
    "aiRaidBehindSeconds": ([0.5, 1, 1.5, 2], "up"),
    "aiAimError":          ([120, 80, 45, 20], "down"),
}
SHARED = {"aiCrystalReach": 300, "aiFaceSpread": 0.8, "aiPlateRange": 900}

g.emit_asset("Assets/_SO_Assets/Games/CaveInSettings.asset", G_ASSET["CaveInSettings"],
             lib.header_for(G_SCRIPT["CaveInSettingsSO"], "CaveInSettings")
             + "".join(mk.ladder_yaml(n, LADDERS) for n in LADDERS)
             + "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in SHARED.items()))

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 2 is a RULE: a race against the arena (Shatterplane's shape) - a solo pilot races the AI.
# No colons in the description (the Serpentine trap).
DESCRIPTION = ("Meerkats only - cave in the plates hanging in the cell. Flying into a plate breaks nothing. Form a "
               "soliton beside one and it swallows the prisms around it and fires them out of its far end, and every "
               "one that hits another prism breaks both. Line two plates up and one tunnel caves in both. First team "
               "to break the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameCaveIn"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameCaveIn") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Cave-in
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameCaveIn
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Meerkat']}, type: 2}}
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_CaveIn.asset", G_ASSET["GameToastConfigCaveIn"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_CaveIn") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(83, "<b>{0}</b> has caved in {1} prisms", tint_domain=1, domain_names=0, every_n=200) +
             lib.toast(128, "{0} is a quarter of the way to {2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Form a soliton beside a plate - it swallows the prisms and fires them out to smash "
                       "the rest", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigCaveIn"])


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


CELL_BLOCK = "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n"
SPAWN_RING = int(sb.SPAWN_RING)
SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
         f"  spawnRingRadiusFloor: {SPAWN_RING}\n  spawnFormation: 0\n")
AI_LINE = f"  - vesselClass: {MEERKAT_ID}\n    PlayerName: AI"

# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_CaveIn.asset", G_ASSET["ModePreviewCaveIn"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_CaveIn") + f"""  Mode: {MODE_ID}
  Notes: 'Shatterplane''s cell at each intensity - billboards, the gallery, the shelves and the reliquary. A race to a
    prism target; a solo preview teaches tunnelling into a plate with a soliton. Vessel pinned to Meerkat({MEERKAT_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {MEERKAT_ID}
  ObjectiveText: Form a soliton beside a plate - it swallows and fires its prisms
  ObjectiveMetric: {METRIC_PRISMS_DESTROYED}
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RING}
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewCaveIn"])

# ── 7. Scene: Shatterplane's, with the identity and the AI hulls swapped ──────
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["ShatterplanePrismTurnMonitor"], G_SCRIPT["CaveInPrismTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["ShatterplaneController"], G_SCRIPT["CaveInController"], "controller")
    scene = lib.swap_guid(scene, DONOR["ShatterplaneSettings"], G_ASSET["CaveInSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["ShatterplaneScoringRule"], G_ASSET["CaveInScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {BRITTLESTAR_ID}\n    PlayerName: AI", AI_LINE, "AI templates",
                          expected=4)
    return scene


SCENE_REL = f"{lib.SCENES_DIR}/MinigameCaveIn.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, [CELL_BLOCK, SPAWN, AI_LINE])
errors += scene_errs
g.emit_scene("MinigameCaveIn", G_ASSET["MinigameCaveIn.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameCaveIn"])
g.register_always_unlocked()
g.register_build_scene("MinigameWarren", "MinigameCaveIn", G_ASSET["MinigameCaveIn.unity"])
g.set_end_condition("caveInPrismTarget", after="warrenRoundSeconds", value=TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {TARGET}: a quarter-of-target deficit buys "
                  f"{0.25 * TARGET * COMEBACK_RATE:.2f} levels (< 1)")
for i, c in CENSUS.items():
    if TARGET > 0.5 * c["breakable"]:
        errors.append(f"I{i}: target {TARGET} is more than half of the rung's {c['breakable']} breakable prisms - "
                      f"three domains share one field")

# The load-bearing assumptions, read from the shipped kit and arena (never edited here).
swallow = g.read(mk.SWALLOW)
if "PrismMotion.Annihilate(" not in swallow:
    errors.append("MeerkatSwallow no longer fires through PrismMotion.Annihilate - a fired prism would break nothing")
motion = g.read(PRISM_MOTION)
for needle in ("struck.DamageByCollision(impact, domain, playerName", "mover.DamageByCollision(-impact, domain, playerName"):
    if needle not in motion:
        errors.append(f"PrismMotion.Annihilate no longer has '{needle}' - a cave-in would not be credited to its maker")
spawnable = g.read(SPAWNABLE)
for needle in ("public void CollectStructures(List<ShatterplanePlates.Structure> into)", "public float FrontOffset",
               "public int PlatesPerStack", "public float ShelfGap", "public float PlateWorldRadius"):
    if needle not in spawnable:
        errors.append(f"SpawnableShatterplane no longer has '{needle}' - the AI miners read the plates through it")
if "public readonly bool Screen;" not in g.read(PLATES):
    errors.append("ShatterplanePlates.Structure lost Screen - the AI would mine the super-shielded grilles")
errors += mk.enum_errors(g, "CaveIn", MODE_ID, "CaveInHint", TOAST_HINT)

eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
for needle in (f"DefaultCaveInPrismTarget = {TARGET};", f"public int caveInPrismTarget = {TARGET};",
               f"public int caveInPrismTargetBuild = {TARGET};"):
    if needle not in eco:
        errors.append(f"EndConditionOverridesSO does not carry '{needle}' (TARGET)")

errors += mk.ladder_errors(g.read(SCRIPT_PATHS["CaveInSettingsSO"]), "CaveInSettingsSO", LADDERS, SHARED)
errors += mk.steer_errors(g, [SCRIPT_PATHS["CaveInController"]])
ctrl = g.read(SCRIPT_PATHS["CaveInController"])
for needle in ("SetNucleusWorldRadius", "LayField", "ElementalCrystalImpactor"):
    if needle in ctrl:
        errors.append(f"CaveInController uses '{needle}' - Shatterplane's nucleus and crystal field are the Brittle "
                      f"Star ring's levers, not this mode's")

if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("CaveInController", "CaveInPrismTurnMonitor")},
                     "CaveInSettings": G_ASSET["CaveInSettings"],
                     "CaveInScoringRule": G_ASSET["CaveInScoringRule"],
                     **{f"Shatterplane cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {MEERKAT_ID}\n") != 4 or f"  - vesselClass: {BRITTLESTAR_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Meerkat AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/CaveIn"), guid(f"text/{AR}/CAVEIN.md")] + mk.shared_minted())
