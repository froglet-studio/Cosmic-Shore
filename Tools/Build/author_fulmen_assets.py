#!/usr/bin/env python3
"""
Authors every serialized asset the Fulmen game mode needs (GameModes.Fulmen = 120).

Fulmen is the Stoat's SPACE game - a burn race through Rampage's cactus forest. The Stoat's omni-crystal ability is
FULMEN (Environment/Fulmen/README.md): every omni crystal it takes throws a branching bolt from the cell's centre that
sets the forest burning, and a crystal a branch reaches bolts again. Rampage's crystals are contested in the nucleus, at
the bolt's root. First DOMAIN to the hostile-prism target wins on ScoringMetric.PrismsDestroyed (Rampage's metric and
rule, inherited). See Assets/_Scripts/Controller/Arcade/FULMEN.md.

THE IDS ARE THE ROSTER'S (Docs/ElementalLoop/GAME_KIT_ROSTER.md row 30): GameModes 120, hint toast 195.

THE ARENA IS RAMPAGE'S, referenced read-only - the four forest configs The Bends, Bloomrush and Sirocco reference too.
The scene is cloned from Undertow (butterfly_games_common, Stoat AI templates), Sirocco's way, with Rampage's own spawn
ring. The donor's crystal manager already carries Rampage's per-intensity crystal counts (asserted below). The
controller has no settings and no arena field (the AI needs nothing: crystal seeking IS the loop), so both lines are
stripped from the cloned controller block.

Idempotent and deterministic (arcade_mode_lib): re-running produces byte-identical output.

Run from the repo root:  python3 Tools/Build/author_fulmen_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib              # noqa: E402
import butterfly_games_common as common    # noqa: E402

MODE_ID = 120          # GAME_KIT_ROSTER.md row 30
TOAST_HINT = 195       # GAME_KIT_ROSTER.md row 30
STOAT_ID = lib.VESSEL_CLASS_ID["Stoat"]
g = lib.Generator(MODE_ID, "Fulmen")
guid = lib.guid

SCRIPT_PATHS = {
    "FulmenController":       "Assets/_Scripts/Controller/Arcade/Fulmen/FulmenController.cs",
    "FulmenScoringRuleSO":    "Assets/_Scripts/Controller/Arcade/Fulmen/FulmenScoringRuleSO.cs",
    "FulmenPrismTurnMonitor": "Assets/_Scripts/Controller/Arcade/TurnMonitors/FulmenPrismTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameFulmen":      guid("asset/ArcadeGameFulmen"),
    "FulmenScoringRule":     guid("asset/FulmenScoringRule"),
    "GameToastConfigFulmen": guid("asset/GameToastConfigFulmen"),
    "ModePreviewFulmen":     guid("asset/ModePreviewFulmen"),
    "MinigameFulmen.unity":  guid("asset/MinigameFulmen.unity"),
}

EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING.update(common.DONOR)
EXISTING["Vessel_Stoat"] = lib.VESSELS["Stoat"]
RAMPAGE_DIR = "Assets/_SO_Assets/Cell Configs/Rampage Cell"
RAMPAGE = [lib.existing_guid(f"{RAMPAGE_DIR}/Rampage Cell Config {i}.asset") for i in range(1, 5)]
for i, gd in enumerate(RAMPAGE, 1):
    EXISTING[f"RampageCellConfig{i}"] = gd

# The ability this mode rests on - read, never edited.
FULMEN_CONFIG = "Assets/_SO_Assets/Environment/FulmenConfig.asset"
FULMEN_EFFECT = "Assets/_SO_Assets/Effects/Vessel Crystal Effects/FulmenByCrystalEffect.asset"
STOAT_CONTAINER = "Assets/_SO_Assets/Effects/Effect Containers/VesselContainers/StoatImpactorDataContainer.asset"
EXISTING["FulmenByCrystalEffect"] = lib.existing_guid(FULMEN_EFFECT)

# ── The race ─────────────────────────────────────────────────────────────────
PRISM_TARGET = 1200   # kept in sync with EndConditionOverridesSO.DefaultFulmenPrismTarget (a first cut: nobody has flown it)
# Comeback - a FUNCTION OF THE TARGET. A quarter behind (300 prisms) buys 1.5 element levels.
COMEBACK_RATE = 0.005
SPAWN = common.spawn_block(500, 0, 0)   # Rampage's own ring - 500 outside its nucleus (Sirocco's, read off The Bends)
RAMPAGE_I4_FOREST = 9830                # RAMPAGE.md / CLAUDE.md: the smallest rung's forest
CELL_RADIUS = lib.MEMBRANE_RADIUS       # Rampage's CapsuleMembrane

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameFulmen.asset"

# ── 1. .cs.meta, folder, doc ─────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta("Assets/_Scripts/Controller/Arcade/Fulmen")
g.text_meta("Assets/_Scripts/Controller/Arcade/FULMEN.md")

# ── 2. Scoring rule: metric 5 = PrismsDestroyed (Rampage's rule, inherited) ──
g.emit_asset("Assets/_SO_Assets/Scoring Rules/FulmenScoringRule.asset", G_ASSET["FulmenScoringRule"],
             lib.header_for(G_SCRIPT["FulmenScoringRuleSO"], "FulmenScoringRule") + """  metric: 5
  golfRules: 1
  prismsLeftTemplate: '{count} Prisms Left'
  breakdownTemplate: '{count} Burnt'
  victoryHeader: VICTORY
  victoryLabel: FULMEN TIME
  defeatHeader: DEFEAT
  defeatLabel: PRISMS LEFT
""")

# ── 3. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 2: a race against the ARENA (the Rampage shape) - a solo pilot races the AI for the crystals.
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameFulmen"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameFulmen") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Fulmen
  Description: Stoats only, in Rampage's cactus forest. Race for the omni crystals in the heart - every
    one you take throws a thunderbolt through the forest, and the plants it touches burn. A bolt that
    reaches another crystal throws another bolt. First team to burn the target wins.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background("Fulmen")}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameFulmen
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Stoat']}, type: 2}}
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

# ── 4. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Fulmen.asset", G_ASSET["GameToastConfigFulmen"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Fulmen") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(83, "<b>{0}</b> has burnt {1} prisms", tint_domain=1, domain_names=0, every_n=200) +
             lib.toast(128, "{0} is a quarter of the way to {2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Race for the omni crystals in the heart - every one you take throws a bolt "
                       "that burns the forest", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigFulmen"])

# ── 5. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Fulmen.asset", G_ASSET["ModePreviewFulmen"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Fulmen") + f"""  Mode: {MODE_ID}
  Notes: 'Reuses Rampage''s cactus forest outright - referenced, not forked, exactly as the mode
    does; intensity is the forest''s density and how many crystals the heart holds. What the
    preview teaches is taking the crystal and the bolt it throws. Vessel pinned to Stoat(15).'
  PreviewCell: {{fileID: 11400000, guid: {RAMPAGE[0]}, type: 2}}
  PreviewCellsByIntensity:
{common.cell_list(RAMPAGE)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {STOAT_ID}
  ObjectiveText: Take the omni crystal in the heart
  ObjectiveMetric: 5
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 500
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewFulmen"])

# ── 6. Scene ─────────────────────────────────────────────────────────────────
DONOR_SETTINGS_LINE = f"  settings: {{fileID: 11400000, guid: {common.DONOR['UndertowSettings']}, type: 2}}\n"
DONOR_ARENA_LINE = "  arenaCell: {fileID: 1700000065}\n"


def build_scene():
    scene = common.clone_scene(G_SCRIPT["FulmenController"], G_SCRIPT["FulmenPrismTurnMonitor"],
                               common.DONOR["UndertowSettings"], G_ASSET["FulmenScoringRule"],
                               RAMPAGE, 1, SPAWN, 0, vessel_id=STOAT_ID)
    # FulmenController declares only `rule`: strip the donor's settings and arena fields from its block.
    rule_line = f"  rule: {{fileID: 11400000, guid: {G_ASSET['FulmenScoringRule']}, type: 2}}\n"
    scene = lib.replace_block(scene, DONOR_SETTINGS_LINE + rule_line + DONOR_ARENA_LINE, rule_line,
                              "controller fields")
    return scene


errors = []
scene = common.clone_or_stand_down(g, "MinigameFulmen", G_ASSET["MinigameFulmen.unity"], errors, build_scene)

# ── 7. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameFulmen"])
g.register_always_unlocked()
g.register_build_scene("MinigameSirocco", "MinigameFulmen", G_ASSET["MinigameFulmen.unity"])
end = g.read_current(lib.END_CONDITIONS)
after_key = "shockfrontRippleTarget" if "shockfrontRippleTarget:" in end else "coronaBloomTarget"
g.set_end_condition("fulmenPrismTarget", after=after_key, value=PRISM_TARGET)

# ══ VALIDATE ════════════════════════════════════════════════════════════════
if 0.25 * PRISM_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {PRISM_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * PRISM_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")
if PRISM_TARGET > 0.25 * RAMPAGE_I4_FOREST:
    errors.append("the target would strip most of intensity 4's forest - lower it")
eoc = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultFulmenPrismTarget = {PRISM_TARGET};" not in eoc:
    errors.append("EndConditionOverridesSO.DefaultFulmenPrismTarget disagrees with PRISM_TARGET")

# The load-bearing assumptions: the Stoat still carries Fulmen, a bolt still burns (and DESTROYS, not converts), and a
# bolt rooted at the centre still reaches the forest.
if EXISTING["FulmenByCrystalEffect"] not in g.read(STOAT_CONTAINER):
    errors.append("the Stoat's container no longer carries FulmenByCrystalEffect - no crystal would throw a bolt")
cfg = g.read(FULMEN_CONFIG)


def cfg_num(key):
    m = re.search(rf"^  {key}: ([0-9.]+)$", cfg, re.M)
    if not m:
        errors.append(f"FulmenConfig.asset has no {key}")
        return 0.0
    return float(m.group(1))


if cfg_num("convertOnBurnout") != 0:
    errors.append("Fulmen now CONVERTS what it burns (steals, scores nothing here) - this mode scores destruction")
if cfg_num("omniCrystalsOnly") != 1:
    errors.append("Fulmen no longer fires on omni crystals only - re-read what the heart's crystals do")
if cfg_num("crystalsToFire") > 2:
    errors.append("Fulmen needs more than two crystals to fire - the race would barely bolt; retune the target")
reach = max(cfg_num("rootRangeShare") * CELL_RADIUS, cfg_num("minRootRange")) + cfg_num("burnRadius")
if reach < 0.6 * CELL_RADIUS:
    errors.append(f"a bolt from the centre now reaches only {reach:.0f} u of the {CELL_RADIUS:.0f} u forest")

controller = g.read(SCRIPT_PATHS["FulmenController"])
for banned in ("PerformShipControllerActions", "InputStatus", "FulmenConfig", "SetExternalTargetProvider"):
    if banned in controller:
        errors.append(f"FulmenController mentions {banned} - the AI needs nothing and the mode never touches the bolt")
if "GameToastSituation.FulmenHint" not in controller:
    errors.append("FulmenController does not post its hint")
if f"FulmenHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.FulmenHint is not {TOAST_HINT} (the roster's id)")
if f"Fulmen = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Fulmen is not {MODE_ID} (the roster's id)")

# Rampage's own spawn ring and crystal counts, borrowed with its cell.
bends = g.read(f"{lib.SCENES_DIR}/MinigameBends.unity")
if SPAWN not in bends:
    errors.append("the Rampage-arena spawn ring (read off The Bends) moved - update SPAWN")
if common.cell_list(RAMPAGE) not in bends:
    errors.append("The Bends no longer runs the four Rampage configs in this order")
CRYSTALS = ("  crystalCountByIntensity:\n  - CrystalsPerPlayer: 2\n    ExtraCrystals: 0\n  - CrystalsPerPlayer: 1\n"
            "    ExtraCrystals: 0\n  - CrystalsPerPlayer: 1\n    ExtraCrystals: -1\n  - CrystalsPerPlayer: 0\n"
            "    ExtraCrystals: 1\n")
if CRYSTALS not in g.read(f"{lib.SCENES_DIR}/MinigameRampage.unity"):
    errors.append("Rampage's per-intensity crystal counts moved - re-read them")
if scene is not None:
    if CRYSTALS not in scene:
        errors.append("the cloned scene does not carry Rampage's crystal counts")
    if DONOR_SETTINGS_LINE in scene or DONOR_ARENA_LINE in scene:
        errors.append("the cloned controller still carries the donor's settings / arena fields")

common.assert_scene(errors, scene,
                    must_have={"FulmenController": G_SCRIPT["FulmenController"],
                               "FulmenPrismTurnMonitor": G_SCRIPT["FulmenPrismTurnMonitor"],
                               "FulmenScoringRule": G_ASSET["FulmenScoringRule"],
                               **{f"Rampage {i}": gd for i, gd in enumerate(RAMPAGE, 1)}},
                    must_not_have={**common.DONOR,
                                   **{f"Wildlife cell {i}": gd for i, gd in enumerate(common.DONOR_CELLS, 1)}},
                    vessel_id=STOAT_ID)

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid("folder/Assets/_Scripts/Controller/Arcade/Fulmen"),
                   guid("text/Assets/_Scripts/Controller/Arcade/FULMEN.md")])
