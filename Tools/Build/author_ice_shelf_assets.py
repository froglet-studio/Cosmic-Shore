#!/usr/bin/env python3
"""
Authors every serialized asset the Ice Shelf game mode needs (GameModes.IceShelf = 100, hint toast 175).

Ice Shelf is the Orca's MASS game (the game kit roster, Docs/ElementalLoop/GAME_KIT_ROSTER.md row 10): a timed hold.
The score is the mass standing in your colour at the whistle (VolumeRemaining): the helm's trail, the fast gun's pen
strokes and the helm's danger shield, carried then dropped as lingering danger mass. Highest wins.

NOTHING ABOUT THE ORCA CHANGES (the LOCKED vessel rule). The arena is Glasshouse's four cells, referenced (the
standard cell with a nucleus and its grazing herds). What the mode adds is the MEASUREMENT (IceShelfCensus: the shield
and the pen retire prisms to their pool with no destroyed event, so the running stat would keep every shield ever
raised) and the helm (OrcaArcadeHelm, goal Shelf). The scene is cloned from Glasshouse's (timed, VolumeRemaining, a
controller declaring settings / rule / arenaCell as this one does).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_ice_shelf_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import orca_games_common as orca  # noqa: E402

MODE_ID = 100
TOAST_HINT = 175
TOAST_LEAD = 130          # DomainRaceLeadChanged, posted by the controller's own lead sampler (a timed mode has no target)
ROUND_SECONDS = 90        # Docs/ARCADE_MATCH_LENGTH.md; kept in sync with EndConditionOverridesSO.DefaultIceShelfRoundSeconds
g = lib.Generator(MODE_ID, "IceShelf")
guid = lib.guid
AR = orca.AR

SCRIPT_PATHS = {
    "IceShelfController":      f"{AR}/IceShelf/IceShelfController.cs",
    "IceShelfScoringRuleSO":   f"{AR}/IceShelf/IceShelfScoringRuleSO.cs",
    "IceShelfCensus":          f"{AR}/IceShelf/IceShelfCensus.cs",
    "IceShelfTimeTurnMonitor": f"{AR}/TurnMonitors/IceShelfTimeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_SCRIPT.update(orca.script_guids())
G_ASSET = {
    "ArcadeGameIceShelf":      guid("asset/ArcadeGameIceShelf"),
    "IceShelfScoringRule":     guid("asset/IceShelfScoringRule"),
    "IceShelfSettings":        guid("asset/IceShelfSettings"),
    "GameToastConfigIceShelf": guid("asset/GameToastConfigIceShelf"),
    "ModePreviewIceShelf":     guid("asset/ModePreviewIceShelf"),
    "MinigameIceShelf.unity":  guid("asset/MinigameIceShelf.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Orca"] = orca.VESSEL_ORCA
# Glasshouse's four cells (minted by author_glasshouse_assets.py with the same guid function).
CELLS = [guid(f"asset/GlasshouseCellConfig{i}") for i in (1, 2, 3, 4)]
for i, gd in enumerate(CELLS, 1):
    EXISTING[f"GlasshouseCellConfig{i}"] = gd
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameGlasshouse.unity"
DONOR = {
    "GlasshouseController":      guid("script/GlasshouseController"),
    "GlasshouseTimeTurnMonitor": guid("script/GlasshouseTimeTurnMonitor"),
    "GlasshouseSettings":        guid("asset/GlasshouseSettings"),
    "GlasshouseScoringRule":     guid("asset/GlasshouseScoringRule"),
}
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]
SPAWN_RING = 450          # Glasshouse's: just outside the nucleus

# ── The hold ─────────────────────────────────────────────────────────────────
# Comeback - a FUNCTION OF THE REFERENCE MATCH (timed, so the "target" is a reference domain total): one Orca's trail
# for the whole round (2 x 2 x 5 prisms at Glasshouse's 40/7 a second, the helm's trail spawner) plus two shields
# standing at the whistle (144 prisms of 10 x 1 x 1). A quarter of that behind buys >= 1 element level.
TRAIL_PRISM = 2 * 2 * 5
TRAIL_PRISMS_PER_SECOND = 40.0 / 7.0
SHIELD_PRISMS = 144
SHIELD_PRISM = 10 * 1 * 1
REFERENCE_DOMAIN_VOLUME = ROUND_SECONDS * TRAIL_PRISMS_PER_SECOND * TRAIL_PRISM + 2 * SHIELD_PRISMS * SHIELD_PRISM
COMEBACK_RATE = 0.0004

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameIceShelf.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
shared_minted = orca.emit_shared_metas(g)
g.folder_meta(f"{AR}/IceShelf")
g.text_meta(f"{AR}/ICESHELF.md")

# ── 2. Scoring rule (Glasshouse's, inherited; Ice Shelf's own words) ─────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/IceShelfScoringRule.asset", G_ASSET["IceShelfScoringRule"],
             lib.header_for(G_SCRIPT["IceShelfScoringRuleSO"], "IceShelfScoringRule") +
             """  metric: 12
  golfRules: 0
  volumeTemplate: '{count} Volume'
  gardenTemplate: '{count} shelf'
  victoryHeader: VICTORY
  defeatHeader: DEFEAT
  revealLabel: MASS STANDING
""")

# ── 3. Settings ──────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/IceShelfSettings.asset", G_ASSET["IceShelfSettings"],
             orca.settings_yaml("IceShelfSettings"))

# ── 4. Arcade card (1/1: the arena's herds and the clock are the opposition, rivals optional) ──
DESCRIPTION = ("Orcas only - a timed hold. Your score is the mass standing in your colour when the clock runs out: "
               "your trail, the fast gun's pen strokes, and your danger shield - carried round the hull, then dropped "
               "where it stands for twenty seconds. A shield dropped in the closing seconds still counts at the "
               "whistle. Rival gunners break your mass, and wild herds graze it outside the core. Higher intensities "
               "bring more grazers.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameIceShelf"],
             orca.card_yaml(EXISTING, "ArcadeGameIceShelf", MODE_ID, "Ice Shelf", DESCRIPTION, CARD_REL,
                            "MinigameIceShelf", golf=False, min_players=1, min_domains=1, comeback=COMEBACK_RATE))

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_IceShelf.asset", G_ASSET["GameToastConfigIceShelf"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_IceShelf") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(TOAST_LEAD, "{0} takes the lead - {1} mass standing", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Your score is the mass standing at the whistle - drop your shield late and it "
                       "still counts", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigIceShelf"])

# ── 6. Mode preview (Glasshouse's cells and ring) ────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_IceShelf.asset", G_ASSET["ModePreviewIceShelf"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_IceShelf") + f"""  Mode: {MODE_ID}
  Notes: 'Glasshouse''s cell at each intensity - more grazers outside the core. OPEN-ENDED - it scores on
    mass standing in your colour at the whistle; what a solo preview teaches is the trail and the
    shield, raised and dropped where it stands. Vessel pinned to Orca({orca.ORCA_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{orca.cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {orca.ORCA_ID}
  ObjectiveText: Lay your trail, raise the shield and drop it where it stands
  ObjectiveMetric: 12
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RING}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {SPAWN_RING}\n" for _ in CELLS) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewIceShelf"])

# ── 7. Scene: Glasshouse's, with the identity and the AI hulls swapped (the cells, ring and crystals stay) ──
errors = []
MONITOR = (f"  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['IceShelfTimeTurnMonitor']}, type: 3}}\n"
           "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n  _updateInterval: 1\n"
           f"  duration: {ROUND_SECONDS}\n")


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["GlasshouseTimeTurnMonitor"], G_SCRIPT["IceShelfTimeTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["GlasshouseController"], G_SCRIPT["IceShelfController"], "controller")
    scene = lib.swap_guid(scene, DONOR["GlasshouseSettings"], G_ASSET["IceShelfSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["GlasshouseScoringRule"], G_ASSET["IceShelfScoringRule"], "scoring rule")
    scene = orca.swap_ai_templates(scene, ANGLER_ID)
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + orca.cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", MONITOR]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameIceShelf.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameIceShelf", G_ASSET["MinigameIceShelf.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameIceShelf"])
g.register_always_unlocked()
g.register_build_scene("MinigameGlasshouse", "MinigameIceShelf", G_ASSET["MinigameIceShelf.unity"])
g.set_end_condition("iceShelfRoundSeconds", after="glasshouseRoundSeconds", value=ROUND_SECONDS)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead: a quarter of the reference domain volume "
                  f"({REFERENCE_DOMAIN_VOLUME:,.0f}) behind buys "
                  f"{0.25 * REFERENCE_DOMAIN_VOLUME * COMEBACK_RATE:.2f} element levels (< 1)")
orca.validate_shared(g, errors)
orca.validate_enums(g, errors, "IceShelf", MODE_ID, "IceShelfHint", TOAST_HINT)
if f"DefaultIceShelfRoundSeconds = {ROUND_SECONDS};" not in g.read(
        "Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"):
    errors.append("EndConditionOverridesSO.DefaultIceShelfRoundSeconds differs from ROUND_SECONDS")
if "BaseScale: {x: 2, y: 2, z: 5}" not in g.read(orca.ORCA_PREFAB):
    errors.append("Orca.prefab's trail prism is no longer 2 x 2 x 5 - re-derive REFERENCE_DOMAIN_VOLUME")
# The census: it reads every producer through the live-prism walk and SETS the stat, and the controller runs it at the
# whistle, so the score is what stood at the end.
census = g.read(SCRIPT_PATHS["IceShelfCensus"])
for needle in ("index.CopyLivePrisms(_live)", "p.destroyed", "p.prismProperties.volume", "p.PlayerName"):
    if needle not in census:
        errors.append(f"IceShelfCensus no longer reads '{needle}'")
ctrl = g.read(SCRIPT_PATHS["IceShelfController"])
for needle in ("stats.VolumeRemaining = standing;", "// The whistle's census", "GameToastSituation.IceShelfHint",
               "Goal.Shelf"):
    if needle not in ctrl:
        errors.append(f"IceShelfController no longer has '{needle}'")
if "public int CopyLivePrisms(List<Prism> results)" not in g.read(
        "Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs"):
    errors.append("PrismSpatialIndex.CopyLivePrisms(List<Prism>) is not what the census calls")
# The Glasshouse rule's fields this asset fills.
glass_rule = g.read(f"{AR}/Glasshouse/GlasshouseScoringRuleSO.cs")
for field in ("volumeTemplate", "gardenTemplate", "victoryHeader", "defeatHeader", "revealLabel"):
    if f"string {field}" not in glass_rule:
        errors.append(f"GlasshouseScoringRuleSO no longer serializes '{field}' - IceShelfScoringRule.asset fills it")

orca.validate_scene(errors, scene,
                    present={**{k: G_SCRIPT[k] for k in ("IceShelfController", "IceShelfTimeTurnMonitor")},
                             "IceShelfSettings": G_ASSET["IceShelfSettings"],
                             "IceShelfScoringRule": G_ASSET["IceShelfScoringRule"],
                             **{f"Glasshouse cell {i}": gd for i, gd in enumerate(CELLS, 1)}},
                    gone=DONOR, donor_class=ANGLER_ID)

g.finish(errors, referenced=EXISTING,
         minted=[G_SCRIPT[k] for k in SCRIPT_PATHS] + list(G_ASSET.values()) + shared_minted
                + [guid(f"folder/{AR}/IceShelf"), guid(f"text/{AR}/ICESHELF.md")])
