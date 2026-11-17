#!/usr/bin/env python3
"""
Authors every serialized asset the Riposte game mode needs (GameModes.Riposte = 97, hint toast 172: the ids
Docs/ElementalLoop/GAME_KIT_ROSTER.md reserved for row 7).

Riposte is the Swordfish's CHARGE game (Garrett, 2026-11-15: "make all the missing games you can"): a sword duel in
JACKSTRAW's heap of pick-up sticks (the same four cells, read, never edited). A STRIKE - the Swordfish's sword reaching a
rival hull, a Strike-class combat hit that lands only while the Swordfish is the faster (VesselCombatHitBySword,
requireFasterThanVictim) - is one point. First DOMAIN to the target wins.

The heap is the lever: the Swordfish's speed is its ramp, which winds up only on a straight line, and the sticks break
straight lines. The blade cuts a plain stick and the hull rams one for free, so a pilot cuts a lane to wind up in; the
shielded knots take two strokes, the red tips debuff a hull that rams them, and the I4 ironwood binds a blade that is
not energized. Charge scales the petals a hit takes (x1 -> x2.5) and Sword Steal (Charge 5) pockets them.

REUSED, NOT COPIED: RiposteController subclasses JackstrawController (one seam added there: the hint it posts),
RiposteScoringRuleSO subclasses JackstrawScoringRuleSO (its own words), the settings asset is a JackstrawSettingsSO
(its own hunters' numbers), and the scene is cloned from Jackstraw's with the identity and the four AI hulls swapped.
Nothing about the vessel changes (the LOCKED mode rule).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_riposte_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402  (plain_scalar_errors, yaml_parse_errors)

MODE_ID = 97
TOAST_HINT = 172
g = lib.Generator(MODE_ID, "Riposte")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "RiposteController":        f"{AR}/Riposte/RiposteController.cs",
    "RiposteScoringRuleSO":     f"{AR}/Riposte/RiposteScoringRuleSO.cs",
    "RiposteStrikeTurnMonitor": f"{AR}/TurnMonitors/RiposteStrikeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameRiposte":      guid("asset/ArcadeGameRiposte"),
    "RiposteScoringRule":     guid("asset/RiposteScoringRule"),
    "RiposteSettings":        guid("asset/RiposteSettings"),
    "GameToastConfigRiposte": guid("asset/GameToastConfigRiposte"),
    "ModePreviewRiposte":     guid("asset/ModePreviewRiposte"),
    "MinigameRiposte.unity":  guid("asset/MinigameRiposte.unity"),
}
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["JackstrawSettingsSO"] = lib.existing_guid(f"{AR}/Jackstraw/JackstrawSettingsSO.cs")
EXISTING["Vessel_Swordfish"] = lib.VESSELS["Swordfish"]

DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameJackstraw.unity"
DONOR_PREVIEW = "Assets/_SO_Assets/Mode Previews/ModePreview_Jackstraw.asset"
DONOR = {
    "JackstrawController":        guid("script/JackstrawController"),
    "JackstrawStrikeTurnMonitor": guid("script/JackstrawStrikeTurnMonitor"),
    "JackstrawSettings":          guid("asset/JackstrawSettings"),
    "JackstrawScoringRule":       guid("asset/JackstrawScoringRule"),
}
THRESHER_ID = lib.VESSEL_CLASS_ID["Thresher"]
SWORDFISH_ID = lib.VESSEL_CLASS_ID["Swordfish"]
SWORD_HIT = "Assets/_SO_Assets/Effects/Vessel Skimmer Effects/VesselCombatHitBySword.asset"
SWORD_CONTAINER = "Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/SwordfishForceFieldSkimmerImpactorDataContainer.asset"
COMBAT_CHANNEL = "45fb43a0cddb5cc3cc4dc448df994152"

# ── The duel ─────────────────────────────────────────────────────────────────
STRIKE_TARGET = 6         # kept in sync with EndConditionOverridesSO.DefaultRiposteStrikeTarget
STRIKE_POINTS = 1
# A quarter of the race behind (1.5 strikes) x 0.7 = 1.05 element levels. On the Swordfish, Charge scales the petals a
# hit takes and Time the sword's rate, so the comeback lands on the fight itself.
COMEBACK_RATE = 0.7

# The hunters (JackstrawSettingsSO; index = AI difficulty - 1). The Thresher's ball TRAILS, so Jackstraw aims its runs
# off the rival; the Swordfish's bill LEADS by 48-80 u, so a run aimed through the rival is the strike. Smaller misses,
# and a longer overshoot so the hull keeps its speed through the pass (the hit lands only while it is the faster).
LADDERS = {
    "aiRetargetSeconds":          ([1.2, 0.9, 0.6, 0.4], "down"),
    "aiHuntRange":                ([600, 800, 1000, 1400], "up"),
    "aiLeadSeconds":              ([0, 0.15, 0.3, 0.45], "up"),
    "aiMissRadius":               ([24, 14, 6, 0], "down"),
    "aiRefuelWhenSilencedChance": ([0, 0.5, 1, 1], "up"),
    "aiSilencedPreyFocus":        ([1, 1, 1.5, 2], "up"),
}
SHARED = dict(progressSampleSeconds=0.5, crystalsPerClearing=3, clearingFill=0.5, crystalRegrowSeconds=8,
              crystalSeed=97, aiOvershoot=120, aiHumanFocus=2)


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


DISPLAY = "Riposte"
DESCRIPTION = """Swordfish only - a sword duel in a heap of pick-up sticks. Your sword lands on a
    rival only while you are the faster, and your ramp winds up only on a straight line, so cut a lane
    through the sticks - the blade cuts them and the hull rams them - wind up and run your bill
    through a rival. Each strike is one point. Crystals float in the clearings. Higher intensities
    grow the heap, cross the sticks into jacks, add red tips and ironwood that binds an unlit blade.
    First team to the target wins."""

# ── 1. metas ────────────────────────────────────────────────────────────────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Riposte")
g.text_meta(f"{AR}/RIPOSTE.md")

# ── 2. Scoring rule (Jackstraw's, inherited; Riposte's own words) ───────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/RiposteScoringRule.asset", G_ASSET["RiposteScoringRule"],
             lib.header_for(G_SCRIPT["RiposteScoringRuleSO"], "RiposteScoringRule") + f"""  metric: 8
  golfRules: 1
  strikePoints: {STRIKE_POINTS}
  otherHitPoints: 0
  strikesLeftTemplate: '{{count}} Strikes Left'
  strikesLandedTemplate: '{{count}} strikes'
  victoryHeader: VICTORY
  victoryLabel: DUEL TIME
  defeatHeader: DEFEAT
  defeatLabel: STRIKES LEFT
""")

# ── 3. Settings (a JackstrawSettingsSO: the hunters' sword-length numbers) ──
g.emit_asset("Assets/_SO_Assets/Games/RiposteSettings.asset", G_ASSET["RiposteSettings"],
             lib.header_for(EXISTING["JackstrawSettingsSO"], "RiposteSettings") +
             f"  progressSampleSeconds: {lib.num(SHARED['progressSampleSeconds'])}\n"
             f"  crystalsPerClearing: {SHARED['crystalsPerClearing']}\n"
             f"  clearingFill: {lib.num(SHARED['clearingFill'])}\n"
             f"  crystalRegrowSeconds: {SHARED['crystalRegrowSeconds']}\n"
             f"  crystalSeed: {SHARED['crystalSeed']}\n" +
             "".join(ladder_yaml(n) for n in LADDERS) +
             f"  aiOvershoot: {SHARED['aiOvershoot']}\n  aiHumanFocus: {SHARED['aiHumanFocus']}\n")

# ── 4. Card ─────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/ArcadeGameRiposte.asset", G_ASSET["ArcadeGameRiposte"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameRiposte") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: {DISPLAY}
  Description: {DESCRIPTION}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background('Riposte')}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameRiposte
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Swordfish']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ───────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Riposte.asset", G_ASSET["GameToastConfigRiposte"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Riposte") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(86, "<b>{0}</b> ran a rival through!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} strikes", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} strikes", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Your sword lands only while you are the faster - cut a straight lane, wind up, "
                       "run your bill through a rival", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigRiposte"])

# ── 6. Preview: Jackstraw's heap at each intensity, the Swordfish flying it ──
errors = []
donor_preview = g.read(DONOR_PREVIEW)
m = re.search(r"^  PreviewCell:.*?(?=^  Vessel: )", donor_preview, re.M | re.S)
spawn = re.search(r"^  ObjectiveMetric: 8\n(.*)\Z", donor_preview, re.M | re.S)
if not m or not spawn:
    errors.append("ModePreview_Jackstraw no longer has the shape this generator copies (PreviewCell .. Vessel, ObjectiveMetric 8 ..)")
    cells_block, spawn_block = "", ""
else:
    cells_block, spawn_block = m.group(0), spawn.group(1)
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Riposte.asset", G_ASSET["ModePreviewRiposte"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Riposte") + f"""  Mode: {MODE_ID}
  Notes: 'Jackstraw''s heap at each intensity, flown by the Swordfish. OPEN-ENDED - it scores on striking rival
    pilots and a solo preview has none; what it teaches is cutting a straight lane through the sticks to wind up in.
    Vessel pinned to Swordfish({SWORDFISH_ID}).'
""" + cells_block + f"""  Vessel: {SWORDFISH_ID}
  ObjectiveText: Cut a lane, wind up and run your bill through a rival
  ObjectiveMetric: 8
""" + spawn_block)
g.register_preview(G_ASSET["ModePreviewRiposte"])


# ── 7. Scene: Jackstraw's, with the identity and the AI hulls swapped ───────
def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["JackstrawStrikeTurnMonitor"], G_SCRIPT["RiposteStrikeTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["JackstrawController"], G_SCRIPT["RiposteController"], "controller")
    scene = lib.swap_guid(scene, DONOR["JackstrawSettings"], G_ASSET["RiposteSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["JackstrawScoringRule"], G_ASSET["RiposteScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {THRESHER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {SWORDFISH_ID}\n    PlayerName: AI", "AI templates", expected=4)
    taken = htr.project_network_hashes(SCENE_REL)
    donor_hashes = sorted({int(h) for h in re.findall(r"GlobalObjectIdHash: (\d+)", scene)} - {0})
    for i, old in enumerate(donor_hashes):
        n = int(guid(f"MinigameRiposte/hash/{i}")[:8], 16)
        while n == 0 or n in taken:
            n = (n * 1103515245 + 12345) & 0xFFFFFFFF
        taken.add(n)
        scene = lib.swap_guid(scene, f"GlobalObjectIdHash: {old}\n", f"GlobalObjectIdHash: {n}\n",
                              f"network hash {old}", expected=scene.count(f"GlobalObjectIdHash: {old}\n"))
    return scene


SCENE_REL = f"{lib.SCENES_DIR}/MinigameRiposte.unity"
AUTHORED_BLOCKS = [f"  - vesselClass: {SWORDFISH_ID}\n    PlayerName: AI"]
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigameRiposte", G_ASSET["MinigameRiposte.unity"], scene)

# ── 8. Registries ───────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameRiposte"])
g.register_always_unlocked()
g.register_build_scene("MinigameJackstraw", "MinigameRiposte", G_ASSET["MinigameRiposte.unity"])
g.set_end_condition("riposteStrikeTarget", after="jackstrawStrikeTarget", value=STRIKE_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * STRIKE_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {STRIKE_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * STRIKE_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")
if f"Riposte = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Riposte is not the roster's reserved {MODE_ID}")
if f"RiposteHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.RiposteHint is not the roster's reserved {TOAST_HINT}")
if f"DefaultRiposteStrikeTarget = {STRIKE_TARGET};" not in g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"):
    errors.append("EndConditionOverridesSO.DefaultRiposteStrikeTarget differs from STRIKE_TARGET")
if "case GameModes.Riposte:" not in g.read("Assets/_Scripts/UI/MiniGameHUD.cs"):
    errors.append("MiniGameHUD.CreateObjectiveProviderForGameMode has no Riposte case")
if f"{MODE_ID}: 'MinigameRiposte'" not in g.read("Tools/Build/author_preview_spawns.py"):
    errors.append("author_preview_spawns.py SCENE_FOR_MODE has no Riposte row")

# The load-bearing facts: a sword hit is a Strike on the combat channel, only while faster, and the Swordfish's sword
# carries that effect. If any moves, the rule pays nothing or the design's premise is gone.
hit = g.read(SWORD_HIT)
for needle in ("  hitClass: 5\n", f"onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_CHANNEL}, type: 2}}",
               "  requireFasterThanVictim: 1\n"):
    if needle not in hit:
        errors.append(f"VesselCombatHitBySword.asset no longer has '{needle.strip()}' - re-check how a sword hit scores")
if lib.existing_guid(SWORD_HIT) not in g.read(SWORD_CONTAINER):
    errors.append("the Swordfish's skimmer container no longer carries VesselCombatHitBySword - its strikes score nothing")
if "Strike = 5," not in g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs"):
    errors.append("CombatHitClass.Strike is no longer 5")
ctl = g.read(SCRIPT_PATHS["RiposteController"])
if "class RiposteController : JackstrawController" not in ctl or "GameToastSituation.RiposteHint" not in ctl:
    errors.append("RiposteController must be Jackstraw's controller with its own hint")
if "protected virtual GameToastSituation HintSituation" not in g.read(f"{AR}/Jackstraw/JackstrawController.cs"):
    errors.append("JackstrawController lost its HintSituation seam")
if EXISTING["JackstrawSettingsSO"] != guid("script/JackstrawSettingsSO"):
    errors.append("JackstrawSettingsSO's script guid is not the one author_jackstraw_assets.py mints")
settings_cs = g.read(f"{AR}/Jackstraw/JackstrawSettingsSO.cs")
for name, (values, harder) in LADDERS.items():
    if f"public float[] {name} =" not in settings_cs:
        errors.append(f"JackstrawSettingsSO has no {name} ladder")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
for field in SHARED:
    if f" {field} = " not in settings_cs:
        errors.append(f"JackstrawSettingsSO has no field {field}")

if scene is not None:
    for name, gd in G_SCRIPT.items():
        if name != "RiposteScoringRuleSO" and gd not in scene:
            errors.append(f"scene missing {name}")
    for name in ("RiposteSettings", "RiposteScoringRule"):
        if G_ASSET[name] not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {SWORDFISH_ID}\n") != 4 or f"  - vesselClass: {THRESHER_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Swordfish AI templates")
    taken = htr.project_network_hashes(SCENE_REL)
    for h in re.findall(r"GlobalObjectIdHash: (\d+)", scene):
        if int(h) != 0 and int(h) in taken:
            errors.append(f"scene GlobalObjectIdHash {h} is also carried by another scene or prefab")

errors += htr.plain_scalar_errors("Description", DESCRIPTION)
errors += htr.yaml_parse_errors(g.files)
g.finish(errors, referenced={"Vessel": EXISTING["Vessel_Swordfish"]},
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()))
