#!/usr/bin/env python3
"""
Authors every serialized asset the Landslide game mode needs (GameModes.Landslide = 123, hint toast 198: the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md row 33 reserved, never "the next free one").

Landslide is the Rhino's SPACE game: a demolition race through Cleave's four arenas (the Panes, the Swell, the Cage,
the Twistbands - referenced, not forked). The Rhino's hull destroys nothing; every prism it scores is a STRIKE - a
prism the plough throws runs into another and both break, scored to the Rhino (PrismMotion.Annihilate). Cleave's
arenas are corduroys of ribs packed a prism apart, so almost every throw is a strike. SPACE is the Blade (the plough's
width, 14 -> 40 u): a wider blade catches more ribs per pass. First domain to the hostile-prism target wins.

REUSED, NOT FORKED: the match and the AI cage-breakers are CleaveController's (written for the Rhino before the
Swordfish took the sword; LandslideController subclasses it to post its hint), the rule is a CleaveScoringRuleSO asset,
the arena is Cleave's four cell configs. The scene is cloned from Cleave's committed scene. The roster's working name
was "Avalanche"; renamed because Avalanche is the Rhino's retired Charge L5 and the name would mislead.

NOTHING ABOUT THE RHINO CHANGES (the LOCKED vessel rule, Shombith 2026-10-31).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_landslide_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 123          # GAME_KIT_ROSTER.md row 33
HINT_TOAST = 198       # GAME_KIT_ROSTER.md row 33
g = lib.Generator(MODE_ID, "Landslide")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "LandslideController":       f"{AR}/Landslide/LandslideController.cs",
    "LandslidePrismTurnMonitor": f"{AR}/TurnMonitors/LandslidePrismTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameLandslide":      guid("asset/ArcadeGameLandslide"),
    "LandslideScoringRule":     guid("asset/LandslideScoringRule"),
    "GameToastConfigLandslide": guid("asset/GameToastConfigLandslide"),
    "ModePreviewLandslide":     guid("asset/ModePreviewLandslide"),
    "MinigameLandslide.unity":  guid("asset/MinigameLandslide.unity"),
}

EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Rhino"] = lib.VESSELS["Rhino"]
EXISTING["CleaveScoringRuleSO"] = lib.existing_guid(f"{AR}/Scoring/CleaveScoringRuleSO.cs")
CLEAVE_CELL_DIR = "Assets/_SO_Assets/Cell Configs/Cleave Cell"
CELLS = [lib.existing_guid(f"{CLEAVE_CELL_DIR}/Cleave Cell Config {i}.asset") for i in (1, 2, 3, 4)]
for i, c in enumerate(CELLS, 1):
    EXISTING[f"Cleave cell {i}"] = c

DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameCleave.unity"
DONOR = {
    "CleaveController":       lib.existing_guid(f"{AR}/CleaveController.cs"),
    "CleavePrismTurnMonitor": lib.existing_guid(f"{AR}/TurnMonitors/CleavePrismTurnMonitor.cs"),
    "CleaveScoringRule":      lib.existing_guid("Assets/_SO_Assets/Scoring Rules/CleaveScoringRule.asset"),
}
SWORDFISH_ID = lib.VESSEL_CLASS_ID["Swordfish"]
RHINO_ID = lib.VESSEL_CLASS_ID["Rhino"]
PLOUGH_CS = "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Prism Effects/RhinoPloughPrismEffectSO.cs"
BLADE_CS = "Assets/_Scripts/Controller/Vessel/RhinoPloughBlade.cs"
CLEAVE_PREVIEW = "Assets/_SO_Assets/Mode Previews/ModePreview_Cleave.asset"

# ── The race ─────────────────────────────────────────────────────────────────
PRISM_TARGET = 500        # kept in sync with EndConditionOverridesSO.DefaultLandslidePrismTarget
# Every rung holds 14,000-16,500 prisms (Cleave's measured census); three domains' targets must fit the hostile pool
# with room to spare, since the arenas never regrow (asserted below).
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (125 prisms) buys
# 1.5 element levels.
COMEBACK_RATE = 0.012

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameLandslide.asset"

# ── 1. .cs.meta, folders, doc ────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Landslide")
g.text_meta(f"{AR}/LANDSLIDE.md")

# ── 2. Scoring rule: a CleaveScoringRuleSO, metric 5 = HostilePrismsDestroyed, golf ──
g.emit_asset("Assets/_SO_Assets/Scoring Rules/LandslideScoringRule.asset", G_ASSET["LandslideScoringRule"],
             lib.header_for(EXISTING["CleaveScoringRuleSO"], "LandslideScoringRule") +
             "  metric: 5\n  golfRules: 1\n")

# ── 3. Arcade card ───────────────────────────────────────────────────────────
DESCRIPTION = ("Rhinos only - four arenas of packed prism ribs to bring down. Your plough breaks nothing it touches, "
               "but every prism it throws into another destroys both, and here the ribs sit a prism apart, so "
               "almost every throw is a strike. Space widens the blade - a wider blade catches more ribs a pass. "
               "Each intensity is a different place - angled panes, wide wavy roads, a cage, twisted bands. First "
               "team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameLandslide"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameLandslide") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Landslide
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameLandslide
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

# ── 4. Toasts (50-52 are posted by the inherited Cleave controller; Landslide words them for itself) ──
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Landslide.asset", G_ASSET["GameToastConfigLandslide"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Landslide") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(50, "{0} is a quarter of the way down - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(51, "{0} is halfway down - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(52, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(HINT_TOAST, "Ploughed prisms break what they hit - a wider blade catches more ribs",
                       domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigLandslide"])

# ── 5. Mode preview: Cleave's arenas and spawn block, a Rhino pinned ─────────
cleave_preview = g.read(CLEAVE_PREVIEW)
tail = cleave_preview[cleave_preview.index("  PreviewCell:"):]
tail = re.sub(r"^  Vessel: -?\d+\n", f"  Vessel: {RHINO_ID}\n", tail, count=1, flags=re.M)
tail = re.sub(r"^  ObjectiveText: .*\n", "  ObjectiveText: Plough the ribs into each other\n", tail, count=1, flags=re.M)
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Landslide.asset", G_ASSET["ModePreviewLandslide"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Landslide") + f"""  Mode: {MODE_ID}
  Notes: 'Cleave''s four arenas (Panes, Swell, Cage, Twistbands), one cell config each, flown
    by a Rhino. The spawn block mirrors the scene''s (author_preview_spawns.py) and is
    load-bearing: the cell has no nucleus. Vessel pinned to Rhino({RHINO_ID}).'
""" + tail)
g.register_preview(G_ASSET["ModePreviewLandslide"])

# ── 6. Scene: Cleave's, with the identity and the AI hulls swapped ───────────
errors = []


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["CleavePrismTurnMonitor"], G_SCRIPT["LandslidePrismTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["CleaveController"], G_SCRIPT["LandslideController"], "controller")
    scene = lib.swap_guid(scene, DONOR["CleaveScoringRule"], G_ASSET["LandslideScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {SWORDFISH_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {RHINO_ID}\n    PlayerName: AI", "AI templates", expected=4)
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n"]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameLandslide.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameLandslide", G_ASSET["MinigameLandslide.unity"], scene)

# ── 7. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameLandslide"])
g.register_always_unlocked()
g.register_build_scene("MinigameCleave", "MinigameLandslide", G_ASSET["MinigameLandslide.unity"])
end = g.read_current(lib.END_CONDITIONS)
g.set_end_condition("landslidePrismTarget",
                    after="windrowRoundSeconds" if "windrowRoundSeconds:" in end else "jackstrawStrikeTarget",
                    value=PRISM_TARGET)

# ══ VALIDATE ════════════════════════════════════════════════════════════════
if 0.25 * PRISM_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {PRISM_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * PRISM_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")
# Three domains' targets in the smallest arena with room to spare (Harvest's rule: a field that never regrows).
for i in (1, 2, 3, 4):
    desc = g.read(f"{CLEAVE_CELL_DIR}/Cleave Cell Config {i}.asset")
    m = re.search(r"(\d+) prisms, (\d+) of them danger", desc)
    if not m:
        errors.append(f"Cleave Cell Config {i} no longer states its census - re-measure the pool")
        continue
    pool = int(m.group(1)) - int(m.group(2))
    if 3 * PRISM_TARGET > pool / 4:
        errors.append(f"Cleave I{i}: {pool} breakable prisms cannot hold three domains' {PRISM_TARGET} with room "
                      f"(three targets must stay under a quarter of the pool)")

plough_cs = g.read(PLOUGH_CS)
for needle in ("PrismMotion.Annihilate", "status.PlayerName"):
    if needle not in plough_cs:
        errors.append(f"RhinoPloughPrismEffectSO no longer has '{needle}' - Landslide scores the plough's strikes "
                      f"to the Rhino's pilot")
if "width" not in g.read(BLADE_CS):
    errors.append("RhinoPloughBlade has no width - the Space lever the card promises is gone")
ability_map = g.read("Assets/Resources/ElementalAbilityMaps/Rhino.asset")
if "Blade" not in ability_map:
    errors.append("the Rhino's ability map no longer names the Blade - re-check which element widens the plough")

if f"Landslide = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Landslide is not {MODE_ID} (the roster's reserved id)")
if f"LandslideHint = {HINT_TOAST}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.LandslideHint is not {HINT_TOAST} (the roster's reserved id)")
if f"DefaultLandslidePrismTarget = {PRISM_TARGET};" not in g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"):
    errors.append("EndConditionOverridesSO.DefaultLandslidePrismTarget differs from PRISM_TARGET")
controller = g.read(SCRIPT_PATHS["LandslideController"])
if "class LandslideController : CleaveController" not in controller or "LandslideHint" not in controller:
    errors.append("LandslideController must subclass CleaveController and post LandslideHint")
cleave = g.read(f"{AR}/CleaveController.cs")
for needle in ("CleaveQuarterCut", "CleaveHalfCut", "CleaveLeaderChanged", "SetExternalTargetProvider",
               "int target = gameData.PrismTargetCount;"):
    if needle not in cleave:
        errors.append(f"CleaveController no longer has '{needle}' - Landslide's toasts and AI ride on it")
for bad in ("PerformShipControllerActions", "InputStatus"):
    if bad in cleave or bad in controller:
        errors.append(f"the Cleave/Landslide AI uses {bad} - an AI Rhino only steers")

if scene is not None:
    for name, gd in {**G_SCRIPT, "LandslideScoringRule": G_ASSET["LandslideScoringRule"],
                     **{f"Cleave cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {RHINO_ID}\n") != 4 or f"  - vesselClass: {SWORDFISH_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Rhino AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Landslide"), guid(f"text/{AR}/LANDSLIDE.md")])
