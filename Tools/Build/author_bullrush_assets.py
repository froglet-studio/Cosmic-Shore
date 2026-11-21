#!/usr/bin/env python3
"""
Authors every serialized asset the Bullrush game mode needs (GameModes.Bullrush = 121, hint toast 196: the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md row 31 reserved, never "the next free one").

Bullrush is the Rhino's CHARGE game: a joust built on speed. The Rhino's Charge ability is the GORE
(RhinoGoreBySkimmerEffectSO): ramming a rival is a Strike-class combat hit, admitted only while the Rhino is faster
than its victim AND above the gore speed (RhinoGoreSpeed: 150 u/s at Charge 0, three times the 50 u/s cruise, down to
50 u/s at Charge 10). So a gore needs a Ramp Spool wind-up on a straight line, and Charge is how short the wind-up can
be. Each gore is one point (a JackstrawScoringRuleSO asset); the first DOMAIN to the target wins.

REUSED, NOT FORKED: the match is JackstrawController's (BullrushController subclasses it to post its own hint), the
arena is Jackstraw's own four heap cells (referenced - the plough throws sticks aside at no cost in speed, so for a
Rhino the heap is cover and its clearings the long straights a gore is wound up on), the AI is JackstrawHunter
(steering only, keyed on each seat's AI difficulty; the hull's own RampBoostAIPolicySO presses the ramp). The scene is
cloned from Jackstraw's committed scene.

NOTHING ABOUT THE RHINO CHANGES (the LOCKED vessel rule, Shombith 2026-10-31).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_bullrush_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

MODE_ID = 121          # GAME_KIT_ROSTER.md row 31
HINT_TOAST = 196       # GAME_KIT_ROSTER.md row 31
g = lib.Generator(MODE_ID, "Bullrush")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "BullrushController":        f"{AR}/Bullrush/BullrushController.cs",
    "BullrushStrikeTurnMonitor": f"{AR}/TurnMonitors/BullrushStrikeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameBullrush":      guid("asset/ArcadeGameBullrush"),
    "BullrushScoringRule":     guid("asset/BullrushScoringRule"),
    "BullrushSettings":        guid("asset/BullrushSettings"),
    "GameToastConfigBullrush": guid("asset/GameToastConfigBullrush"),
    "ModePreviewBullrush":     guid("asset/ModePreviewBullrush"),
    "MinigameBullrush.unity":  guid("asset/MinigameBullrush.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Rhino"] = lib.VESSELS["Rhino"]
EXISTING["JackstrawScoringRuleSO"] = lib.existing_guid(f"{AR}/Jackstraw/JackstrawScoringRuleSO.cs")
EXISTING["JackstrawSettingsSO"] = lib.existing_guid(f"{AR}/Jackstraw/JackstrawSettingsSO.cs")
JACK_CELL_DIR = "Assets/_SO_Assets/Cell Configs/Jackstraw Cell"
CELLS = [lib.existing_guid(f"{JACK_CELL_DIR}/Jackstraw Cell Config {i}.asset") for i in (1, 2, 3, 4)]
for i, c in enumerate(CELLS, 1):
    EXISTING[f"Jackstraw cell {i}"] = c

DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameJackstraw.unity"
DONOR = {
    "JackstrawController":        lib.existing_guid(f"{AR}/Jackstraw/JackstrawController.cs"),
    "JackstrawStrikeTurnMonitor": lib.existing_guid(f"{AR}/TurnMonitors/JackstrawStrikeTurnMonitor.cs"),
    "JackstrawSettings":          lib.existing_guid("Assets/_SO_Assets/Games/JackstrawSettings.asset"),
    "JackstrawScoringRule":       lib.existing_guid("Assets/_SO_Assets/Scoring Rules/JackstrawScoringRule.asset"),
}
THRESHER_ID = lib.VESSEL_CLASS_ID["Thresher"]
RHINO_ID = lib.VESSEL_CLASS_ID["Rhino"]

GORE_EFFECT_CS = "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Skimmer Effects/RhinoGoreBySkimmerEffectSO.cs"
GORE_SPEED = "Assets/_SO_Assets/VesselActions/Rhino/RhinoGoreSpeed.asset"
GORE_HIT = "Assets/_SO_Assets/Effects/Vessel Skimmer Effects/VesselCombatHitByGore.asset"
RHINO_PREFAB = "Assets/_Prefabs/Spacevessels/Rhino.prefab"

# ── The joust ────────────────────────────────────────────────────────────────
GORE_TARGET = 5           # kept in sync with EndConditionOverridesSO.DefaultBullrushGoreTarget
GORE_POINTS = 1
STRIKE_CLASS = 5          # CombatHitClass.Strike - what the gore's VesselCombatHitBySkimmerEffect reports
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (1.25 gores) buys
# 1.25 element levels; on this hull a Charge level lowers the gore speed by 10 u/s.
COMEBACK_RATE = 1.0
CRYSTALS_PER_CLEARING = 3

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameBullrush.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Bullrush")
g.text_meta(f"{AR}/BULLRUSH.md")

# ── 2. Scoring rule: a JackstrawScoringRuleSO with the Rhino's words ─────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/BullrushScoringRule.asset", G_ASSET["BullrushScoringRule"],
             lib.header_for(EXISTING["JackstrawScoringRuleSO"], "BullrushScoringRule") +
             f"""  metric: 8
  golfRules: 1
  strikePoints: {GORE_POINTS}
  otherHitPoints: 0
  strikesLeftTemplate: '{{count}} Gores Left'
  strikesLandedTemplate: '{{count}} gores'
  victoryHeader: VICTORY
  victoryLabel: BULLRUSH TIME
  defeatHeader: DEFEAT
  defeatLabel: GORES LEFT
""")

# ── 3. Mode settings: a JackstrawSettingsSO (index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
# A Rhino hunter hunts further than a Thresher (it needs the run-up), leads its prey less (it rams from behind, it does
# not swing ahead of it) and overshoots further (a gore is a ram THROUGH the rival at speed).
LADDERS = {
    "aiRetargetSeconds":          ([1.4, 1.1, 0.8, 0.5], "down"),
    "aiHuntRange":                ([800, 1000, 1300, 1700], "up"),
    "aiLeadSeconds":              ([0, 0.1, 0.2, 0.3], "up"),
    "aiMissRadius":               ([50, 35, 18, 6], "down"),
    "aiRefuelWhenSilencedChance": ([0, 0.5, 1, 1], "up"),
    "aiSilencedPreyFocus":        ([1, 1, 1.5, 2], "up"),
}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/BullrushSettings.asset", G_ASSET["BullrushSettings"],
             lib.header_for(EXISTING["JackstrawSettingsSO"], "BullrushSettings") +
             f"""  progressSampleSeconds: 0.5
  crystalsPerClearing: {CRYSTALS_PER_CLEARING}
  clearingFill: 0.5
  crystalRegrowSeconds: 8
  crystalSeed: 121
""" + "".join(ladder_yaml(n) for n in LADDERS) + """  aiOvershoot: 140
  aiHumanFocus: 2
""")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: a gore never lands on a team-mate, so a solo lobby has nobody to score on.
DESCRIPTION = ("Rhinos only - a joust in a heap of floating sticks. Ram a rival from behind while you are faster "
               "than them to gore them - each gore is one point and knocks their petals loose. A gore needs speed: "
               "fly straight to wind up the Ramp Spool, and every Charge level lowers the speed a gore needs. The "
               "plough throws the sticks aside without slowing you, so the heap is cover, and the clearings are "
               "where you build the run. Crystals float in the clearings. Higher intensities grow the heap and add "
               "red-tipped sticks that sting. First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameBullrush"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameBullrush") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Bullrush
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameBullrush
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

# ── 5. Toasts (Jackstraw's own hint, 160, is deliberately absent: BullrushController posts 196) ──
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Bullrush.asset", G_ASSET["GameToastConfigBullrush"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Bullrush") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(86, "<b>{0}</b> gored a rival!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} gores", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} gores", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(HINT_TOAST, "Ram a rival from behind, faster than them - fly straight to wind up the Ramp Spool",
                       domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigBullrush"])

# ── 6. Mode preview (Jackstraw's heap at each intensity, spawn block = Jackstraw's scene's) ──
jack_preview = g.read("Assets/_SO_Assets/Mode Previews/ModePreview_Jackstraw.asset")


def preview_tail(src):
    """Jackstraw's preview from SpawnFromCellRing on: the spawn block the cloned scene carries unchanged."""
    i = src.index("  SpawnFromCellRing:")
    return src[i:]


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Bullrush.asset", G_ASSET["ModePreviewBullrush"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Bullrush") + f"""  Mode: {MODE_ID}
  Notes: 'Jackstraw''s heap at each intensity, flown by a Rhino. OPEN-ENDED - it scores on goring
    rival pilots and a solo preview has none; what it teaches is the straight run through the
    sticks that winds up the Ramp Spool. Vessel pinned to Rhino({RHINO_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {RHINO_ID}
  ObjectiveText: Fly straight to wind up, then ram a rival from behind
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
""" + preview_tail(jack_preview))
g.register_preview(G_ASSET["ModePreviewBullrush"])

# ── 7. Scene: Jackstraw's, with the identity and the AI hulls swapped ────────
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["JackstrawStrikeTurnMonitor"], G_SCRIPT["BullrushStrikeTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["JackstrawController"], G_SCRIPT["BullrushController"], "controller")
    scene = lib.swap_guid(scene, DONOR["JackstrawSettings"], G_ASSET["BullrushSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["JackstrawScoringRule"], G_ASSET["BullrushScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {THRESHER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {RHINO_ID}\n    PlayerName: AI", "AI templates", expected=4)
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n"]
SCENE_REL = f"{lib.SCENES_DIR}/MinigameBullrush.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameBullrush", G_ASSET["MinigameBullrush.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameBullrush"])
g.register_always_unlocked()
g.register_build_scene("MinigameJackstraw", "MinigameBullrush", G_ASSET["MinigameBullrush.unity"])
g.set_end_condition("bullrushGoreTarget", after="jackstrawStrikeTarget", value=GORE_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * GORE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {GORE_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * GORE_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing assumptions: the gore is still a Charge-gated contact on rival hulls that reports a Strike, the
# Rhino still carries it, and the gore speed still asks for a wind-up at rest.
gore = g.read(GORE_EFFECT_CS)
for needle in ("IContactAdmission", "public bool AdmitsContact(IVesselStatus attacker, IVesselStatus victim)",
               "JoustSpeedConfigSO goreSpeed", "CombatHitClass.Strike"):
    if needle not in gore:
        errors.append(f"RhinoGoreBySkimmerEffectSO no longer has '{needle}' - Bullrush pays a gore as a Strike-class "
                      f"combat hit gated on speed; re-check how the Rhino hits a hull")
if not g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs").count(f"Strike = {STRIKE_CLASS},"):
    errors.append(f"CombatHitClass.Strike is no longer {STRIKE_CLASS}")
gs = g.read(GORE_SPEED)
if "    Min: 150\n    Max: 50\n    element: 1\n" not in gs:
    errors.append("RhinoGoreSpeed no longer reads 150 -> 50 u/s on Charge - the card copy and BULLRUSH.md quote it")
hit = g.read(GORE_HIT)
for needle, why in ((f"  hitClass: {STRIKE_CLASS}\n", "a gore must report a Strike, the class the rule pays"),
                    ("  onCombatHitLanded: {fileID: 11400000, guid: 45fb43a0cddb5cc3cc4dc448df994152", "the scoring channel"),
                    ("  requireFasterThanVictim: 1\n", "the joust rule: a gore is a ram the Rhino drives")):
    if needle not in hit:
        errors.append(f"VesselCombatHitByGore lost '{needle.strip()}' ({why})")
if lib.existing_guid(GORE_HIT) not in g.read(RHINO_PREFAB) and lib.existing_guid(GORE_HIT) not in \
        g.read("Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/RhinoSkimmerImpactorDataContainer.asset"):
    errors.append("neither the Rhino prefab nor its skimmer container carries VesselCombatHitByGore - nothing gores")

eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultBullrushGoreTarget = {GORE_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultBullrushGoreTarget differs from GORE_TARGET")
if f"Bullrush = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Bullrush is not {MODE_ID} (the roster's reserved id)")
if f"BullrushHint = {HINT_TOAST}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.BullrushHint is not {HINT_TOAST} (the roster's reserved id)")
controller = g.read(SCRIPT_PATHS["BullrushController"])
if "class BullrushController : JackstrawController" not in controller or "BullrushHint" not in controller:
    errors.append("BullrushController must subclass JackstrawController and post BullrushHint")
for bad in ("PerformShipControllerActions", "InputStatus", "SelectedIntensity"):
    if bad in controller:
        errors.append(f"BullrushController uses {bad} - the mode only steers, and the AI reads difficulty, not intensity")

for name, (values, harder) in LADDERS.items():
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
settings_cs = g.read(f"{AR}/Jackstraw/JackstrawSettingsSO.cs")
for name in list(LADDERS) + ["crystalsPerClearing", "clearingFill", "crystalRegrowSeconds", "crystalSeed",
                             "aiOvershoot", "aiHumanFocus", "progressSampleSeconds"]:
    if name not in settings_cs:
        errors.append(f"JackstrawSettingsSO no longer has '{name}' - BullrushSettings authors it")

if scene is not None:
    for name, gd in {**G_SCRIPT, "BullrushSettings": G_ASSET["BullrushSettings"],
                     "BullrushScoringRule": G_ASSET["BullrushScoringRule"],
                     **{f"Jackstraw cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {RHINO_ID}\n") != 4 or f"  - vesselClass: {THRESHER_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Rhino AI templates")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Bullrush"), guid(f"text/{AR}/BULLRUSH.md")])
