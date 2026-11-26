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
import jackstraw_budget as jb  # noqa: E402  (the heap's constants and geometry)
import riposte_yard_budget as yard  # noqa: E402  (the Lists: an exact mirror of SpawnableRiposteYard.cs)

MODE_ID = 97
TOAST_HINT = 172
g = lib.Generator(MODE_ID, "Riposte")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "RiposteController":        f"{AR}/Riposte/RiposteController.cs",
    "RiposteScoringRuleSO":     f"{AR}/Riposte/RiposteScoringRuleSO.cs",
    "RiposteSettingsSO":        f"{AR}/Riposte/RiposteSettingsSO.cs",
    "RiposteDuel":              f"{AR}/Riposte/RiposteDuel.cs",
    "RiposteDuelist":           f"{AR}/Riposte/RiposteDuelist.cs",
    "RiposteDuelTests":         "Assets/_Scripts/Tests/Editor/RiposteDuelTests.cs",
    "RiposteYard":              f"{AR}/Riposte/RiposteYard.cs",
    "SpawnableRiposteYard":     "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableRiposteYard.cs",
    "RiposteYardTests":         "Assets/_Scripts/Tests/Editor/RiposteYardTests.cs",
    "RiposteLanes":             f"{AR}/Riposte/RiposteLanes.cs",
    "RiposteLanesTests":        "Assets/_Scripts/Tests/Editor/RiposteLanesTests.cs",
    "RiposteStrikeTurnMonitor": f"{AR}/TurnMonitors/RiposteStrikeTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameRiposte":      guid("asset/ArcadeGameRiposte"),
    "RiposteScoringRule":     guid("asset/RiposteScoringRule"),
    "RiposteSettings":        guid("asset/RiposteSettings"),
    "GameToastConfigRiposte": guid("asset/GameToastConfigRiposte"),
    "ModePreviewRiposte":     guid("asset/ModePreviewRiposte"),
    "SwordfishAIBoostPolicy_Riposte": guid("asset/SwordfishAIBoostPolicy_Riposte"),
    "MinigameRiposte.unity":  guid("asset/MinigameRiposte.unity"),
}
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Riposte Cell"
for _i in (1, 2, 3, 4):
    G_ASSET[f"RiposteCellConfig{_i}"] = guid(f"asset/RiposteCellConfig{_i}")
    G_ASSET[f"prefab/SpawnableRiposteYard{_i}"] = guid(f"asset/SpawnableRiposteYard{_i}.prefab")
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
# Read, never edited: the heap's prism, the standard 1,200 u cell, and Jackstraw's empty spawn profile.
EXISTING["Prism_prefab"] = "ed9defc56162b4b4588e61c20984b6d9"
EXISTING["CapsuleMembrane_prefab"] = "6e330f85972faf843b8a128e7166f7b5"
EXISTING["Cytoplasm_prefab"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
EXISTING["JackstrawSpawnProfile"] = guid("asset/JackstrawSpawnProfile")
JACKSTRAW_CELLS = [guid(f"asset/JackstrawCellConfig{i}") for i in (1, 2, 3, 4)]
JACKSTRAW_RINGS = [r["spawnRing"] for r in jb.RUNGS]
PRISM_FILEID = 4563009547826722997
MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
SPAWNABLE_GO, SPAWNABLE_TR, SPAWNABLE_MB = 9700000000000201, 9700000000000202, 9700000000000203
YARD_CS = "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableRiposteYard.cs"
MEMBRANE_RADIUS = 1200
LAY_CAPACITY = 4096

# ── The arena: the Lists (RIPOSTE.md section 4) ─────────────────────────────
RUNGS = yard.RUNGS
for _r in RUNGS:
    _r["prefab"] = f"Assets/_Prefabs/Spawnables/SpawnableRiposteYard{_r['i']}.prefab"
    _r["laid"] = yard.build(_r)
# Jackstraw's deltas: a cell's phase rides its own arena's baseline, so a match never starts in the wrong one.
DELTAS = dict(re=700, rx=500, fe=3600, fx=3000, rev=11200, rxv=8000, fev=57600, fxv=48000)
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

# The duelists (RiposteSettingsSO, RiposteDuelist; RIPOSTE.md section 3). Jackstraw's hunter, flown by a Swordfish,
# made a slow knot (30 u/s, nobody winding up). A duelist flies long straight legs: cut sticks for ramp fuel, charge
# through a rival and on past them, run straight out, turn back in. Index = AI difficulty - 1.
DUEL_LADDERS = {
    "duelThrottleFloor":   ([0.7, 0.85, 1, 1], "up"),
    "duelReapBelowFuel":   ([0, 0.03, 0.06, 0.1], "up"),
    "duelReapUntilFuel":   ([0, 0.08, 0.15, 0.25], "up"),
    "duelChargeOvershoot": ([150, 250, 350, 450], "up"),
    "duelExtendDistance":  ([120, 220, 320, 420], "up"),
    # How often a leg reads the arena: a fuel rail flown end to end, a run-out to a lane mouth, a wind-up down the lane.
    "duelLaneUse":         ([0.25, 0.5, 0.85, 1], "up"),
}
DUEL_SHARED = dict(duelPassRadius=240, duelPhaseSeconds=6, duelReapSpreadDegrees=35, duelArriveRadius=90,
                   duelLineUpRadius=40, duelMouthConeDegrees=60, duelLaneBreakDistance=320, duelRailDryGain=0.02)
LADDERS.update(DUEL_LADDERS)

# The AI's ramp in this mode (RampBoostAIPolicySO, Resources/AIBoostPolicies/SwordfishAIBoostPolicy_Riposte, picked by
# AIBoostPolicySO.ForMode). The hull's own policy is a racer's: it presses the ramp inside 12 degrees and brakes to a
# corner speed at its objective. A duelist's objective is a point PAST its rival, so it must arrive at full ramp: a
# wide straight test, a short runway, and a corner speed above the ramp's top so it never brakes for the pass.
# Plan step: charge-arcade (Docs/AI_SYSTEM/AI_PLAN.md). Hand-set from the real-game judge, not a search.
RAMP_POLICY = dict(alignDegrees=30, releaseAlignDegrees=55, stickBand=0.6, releaseStickBand=0.9,
                   engageRunwaySeconds=0.6, releaseRunwaySeconds=0.1, steeringShareOfPlateau=0.9, minHoldSeconds=0.5,
                   minRestSeconds=0.3, minRearmSeconds=0.5, minSpeedGain=20, cornerSpeed=2000, minCornerSpeed=900,
                   brakeMarginUnits=0, brakeLeadSeconds=0, graceShare=0.9, brakeSteerFloor=0.3,
                   throttleSlewPerSecond=4, cornerReleaseDegrees=60)
RAMP_POLICY_REL = "Assets/Resources/AIBoostPolicies/SwordfishAIBoostPolicy_Riposte.asset"
RAMP_POLICY_CS = "Assets/_Scripts/Controller/AI/Boost/RampBoostAIPolicySO.cs"


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
g.folder_meta(CELL_DIR)
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

# ── 3. Settings (a RiposteSettingsSO: Jackstraw's fields, then the duelists') ──
g.emit_asset("Assets/_SO_Assets/Games/RiposteSettings.asset", G_ASSET["RiposteSettings"],
             lib.header_for(G_SCRIPT["RiposteSettingsSO"], "RiposteSettings") +
             f"  progressSampleSeconds: {lib.num(SHARED['progressSampleSeconds'])}\n"
             f"  crystalsPerClearing: {SHARED['crystalsPerClearing']}\n"
             f"  clearingFill: {lib.num(SHARED['clearingFill'])}\n"
             f"  crystalRegrowSeconds: {SHARED['crystalRegrowSeconds']}\n"
             f"  crystalSeed: {SHARED['crystalSeed']}\n" +
             "".join(ladder_yaml(n) for n in LADDERS if n not in DUEL_LADDERS) +
             f"  aiOvershoot: {SHARED['aiOvershoot']}\n  aiHumanFocus: {SHARED['aiHumanFocus']}\n" +
             "".join(ladder_yaml(n) for n in DUEL_LADDERS) +
             "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in DUEL_SHARED.items()))

# ── 3a. The AI's ramp in this mode ──────────────────────────────────────────
g.emit_asset(RAMP_POLICY_REL, G_ASSET["SwordfishAIBoostPolicy_Riposte"],
             lib.header_for(lib.existing_guid(RAMP_POLICY_CS), "SwordfishAIBoostPolicy_Riposte") +
             "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in list(RAMP_POLICY.items())[:6]) +
             "  disabledInModes: []\n  requireAIPlayer: 0\n" +
             "".join(f"  {k}: {lib.num(float(v))}\n" for k, v in list(RAMP_POLICY.items())[6:]))

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
  MinPlayersAllowed: 4
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

# ── 5a. The arena: one SpawnableRiposteYard prefab and one cell config per rung ──
YARD_FIELDS = ("laneCount", "laneRadius", "railsPerLane", "gateEveryNthLane", "gatesPerLane", "looseSticks",
               "looseJacks", "thornBand", "ironwoodCoreRadius")
for r in RUNGS:
    g.emit_prefab(r["prefab"], G_ASSET[f"prefab/SpawnableRiposteYard{r['i']}"], f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{SPAWNABLE_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {SPAWNABLE_TR}}}
  - component: {{fileID: {SPAWNABLE_MB}}}
  m_Layer: 0
  m_Name: SpawnableRiposteYard{r['i']}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{SPAWNABLE_TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SPAWNABLE_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{SPAWNABLE_MB}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SPAWNABLE_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableRiposteYard']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: {r['seed']}
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: {r['i']}
  prism: {{fileID: {PRISM_FILEID}, guid: {EXISTING['Prism_prefab']}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
  heapRadius: {r['heapRadius']}
  stickCount: 0
  jackCount: 0
  centreClearingRadius: {r['centreClearingRadius']}
  ringClearingCount: {r['ringClearingCount']}
  ringClearingDistance: {r['ringClearingDistance']}
  ringClearingRadius: {r['ringClearingRadius']}
  dangerTipEveryNth: 0
  ironwoodEveryNth: 0
""" + "".join(f"  {k}: {r[k]}\n" for k in YARD_FIELDS))

for r in RUNGS:
    i, c = r["i"], r["laid"]["census"]
    count, volume = c["prisms"], round(c["volume"])
    desc = (f"{r['label']} - intensity {i} of Riposte: {r['blurb']}. {c['prisms']} prisms, all neutral "
            f"({c['shielded']} shielded, {c['danger']} danger; {c['railPrisms']} of them fuel rails), heap radius "
            f"{r['heapRadius']} inside the standard {MEMBRANE_RADIUS} u cell, spawn ring {r['spawnRing']}. Crystals "
            f"float in the clearings, laid by the controller. NO NUCLEUS, no flora, no fauna. Authored by "
            f"Tools/Build/author_riposte_assets.py from riposte_yard_budget.py - re-run it rather than hand-editing.")
    g.emit_asset(f"{CELL_DIR}/Riposte Cell Config {i}.asset", G_ASSET[f"RiposteCellConfig{i}"],
                 lib.header_for(EXISTING["CellConfigDataSO"], f"Riposte Cell Config {i}") + f"""  CellName: Riposte
  Description: {lib.wrap_yaml_scalar(desc)}
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {i}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane_prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm_prefab']}, type: 3}}
  CytoplasmShardDistance: 0
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {EXISTING['JackstrawSpawnProfile']}, type: 2}}
  EnvironmentPrefab: {{fileID: {SPAWNABLE_MB}, guid: {G_ASSET[f'prefab/SpawnableRiposteYard{i}']}, type: 3}}
  EnvironmentIntensity: {i}
  SenseRadiusOverride: 0
  PhaseThresholds:
    RestlessEnter: {count + DELTAS['re']}
    RestlessExit: {count + DELTAS['rx']}
    FrenzyEnter: {count + DELTAS['fe']}
    FrenzyExit: {count + DELTAS['fx']}
    RestlessEnterVolume: {volume + DELTAS['rev']}
    RestlessExitVolume: {volume + DELTAS['rxv']}
    FrenzyEnterVolume: {volume + DELTAS['fev']}
    FrenzyExitVolume: {volume + DELTAS['fxv']}
""")

CELLS = [G_ASSET[f"RiposteCellConfig{i}"] for i in (1, 2, 3, 4)]
RINGS = [r["spawnRing"] for r in RUNGS]


def ring_block(rings):
    return (f"  spawnRingRadiusFloor: {rings[0]}\n  spawnRingRadiusFloorByIntensity:\n"
            + "".join(f"  - {v}\n" for v in rings))


def own_cells(text, label, each=1):
    """Point a text that names Jackstraw's four cells at Riposte's. Stands down once it already does."""
    for old, new in zip(JACKSTRAW_CELLS, CELLS):
        if old in text:
            text = lib.swap_guid(text, old, new, f"{label}: cell", expected=text.count(old))
    return text


# ── 6. Preview: the Lists at each intensity, the Swordfish flying it ──
errors = []
donor_preview = g.read(DONOR_PREVIEW)
m = re.search(r"^  PreviewCell:.*?(?=^  Vessel: )", donor_preview, re.M | re.S)
spawn = re.search(r"^  ObjectiveMetric: 8\n(.*)\Z", donor_preview, re.M | re.S)
if not m or not spawn:
    errors.append("ModePreview_Jackstraw no longer has the shape this generator copies (PreviewCell .. Vessel, ObjectiveMetric 8 ..)")
    cells_block, spawn_block = "", ""
else:
    cells_block, spawn_block = own_cells(m.group(0), "preview"), spawn.group(1)
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Riposte.asset", G_ASSET["ModePreviewRiposte"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Riposte") + f"""  Mode: {MODE_ID}
  Notes: 'The Lists at each intensity, flown by the Swordfish. OPEN-ENDED - it scores on striking rival
    pilots and a solo preview has none; what it teaches is winding up down a lane and cutting its rails for fuel.
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
if scene is not None:
    # Riposte's own cells and spawn rings (the scene was cloned with Jackstraw's). Both stand down once applied.
    scene = own_cells(scene, "scene")
    if ring_block(JACKSTRAW_RINGS) in scene:
        scene = scene.replace(ring_block(JACKSTRAW_RINGS), ring_block(RINGS))
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
settings_cs = g.read(f"{AR}/Jackstraw/JackstrawSettingsSO.cs") + g.read(SCRIPT_PATHS["RiposteSettingsSO"])
for name, (values, harder) in LADDERS.items():
    if f"public float[] {name} =" not in settings_cs:
        errors.append(f"JackstrawSettingsSO has no {name} ladder")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
for field in list(SHARED) + list(DUEL_SHARED):
    if f" {field} = " not in settings_cs:
        errors.append(f"the settings classes have no field {field}")
if "class RiposteSettingsSO : JackstrawSettingsSO" not in settings_cs:
    errors.append("RiposteSettingsSO must extend JackstrawSettingsSO (the scene's settings field is typed as the base)")
for lo, hi in zip(DUEL_LADDERS["duelReapBelowFuel"][0], DUEL_LADDERS["duelReapUntilFuel"][0]):
    if hi < lo:
        errors.append("duelReapUntilFuel must be at least duelReapBelowFuel on every rung, or a pilot cuts forever")

# The AI is input-only and the vessel is untouched (the LOCKED mode rule): the duelist may steer and ask for
# throttle, nothing else. The ramp's mode variant must be the hull's own policy class under the name ForMode loads.
duelist_cs = g.read(SCRIPT_PATHS["RiposteDuelist"])
for banned in ("PerformShipControllerActions", "InputStatus", "TriggerAnalog", "SelectedIntensity", "ChangeResourceAmount"):
    if banned in duelist_cs:
        errors.append(f"RiposteDuelist uses {banned}: it steers and sets a throttle floor, and its level is the seat's AI difficulty")
if "yard.CollectLines(" not in ctl or "CollectLines(List<RiposteLanes.Line>" not in g.read(YARD_CS):
    errors.append("RiposteController must hand its duelists the arena's lanes and rails (SpawnableRiposteYard.CollectLines)")
for needed in ("SetExternalTargetProvider", "SetThrottleFloor", "AIDifficultyFor", "RiposteLanes.Nearest(",
               "RiposteLanes.MouthAhead(", "RiposteLanes.RunIn("):
    if needed not in duelist_cs:
        errors.append(f"RiposteDuelist no longer calls {needed}")
# A human seat the AI holds (an idle guest, the AI-flies toggle) duels like a seat, not on the stock autopilot.
if "IAIPilotArming" not in ctl or "_duelist.ArmOne(" not in ctl or "HeldByAutopilot(p)" not in duelist_cs:
    errors.append("RiposteController must arm a human hull the AI holds as a duelist (IAIPilotArming, "
                  "RiposteDuelist.ArmOne, HeldByAutopilot at the start)")
if "_duelist.Arm(" not in ctl or "protected virtual void ArmAI()" not in g.read(f"{AR}/Jackstraw/JackstrawController.cs"):
    errors.append("RiposteController must arm its duelists through JackstrawController's ArmAI seam")
policy_cs = g.read(RAMP_POLICY_CS) + g.read("Assets/_Scripts/Controller/AI/Boost/AIBoostPolicySO.cs")
for field in RAMP_POLICY:
    if not re.search(r"\b" + field + r"\b", policy_cs):
        errors.append(f"RampBoostAIPolicySO has no field {field}")
hull_policy = "Assets/_SO_Assets/AI Boost Policies/SwordfishAIBoostPolicy.asset"
if lib.existing_guid(RAMP_POLICY_CS) not in g.read(hull_policy):
    errors.append("the Swordfish's own boost policy is no longer a RampBoostAIPolicySO: ForMode would ignore the Riposte variant")
if lib.existing_guid(hull_policy) not in g.read("Assets/_Prefabs/Spacevessels/Swordfish.prefab"):
    errors.append("Swordfish.prefab no longer wires SwordfishAIBoostPolicy: the Riposte variant's name would not be looked up")
if RAMP_POLICY["cornerSpeed"] < 840 * 1.1:
    errors.append("the Riposte ramp's cornerSpeed must stay above the ramp's top (840 u/s) or the AI brakes before its rival")

if scene is not None:
    for name, gd in G_SCRIPT.items():
        if name in ("RiposteController", "RiposteStrikeTurnMonitor") and gd not in scene:
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

# ── The Lists: every placement rule, checked on the laid arena (RIPOSTE.md section 4) ──
yard_cs = g.read(YARD_CS) + g.read(SCRIPT_PATHS["RiposteYard"])
for needle in (f"PicketPrisms = {yard.PICKET_PRISMS};", f"PicketGap = {lib.num(yard.PICKET_GAP)}f;",
               f"RailStandoff = {lib.num(yard.RAIL_STANDOFF)}f;", f"RailStart = {lib.num(yard.RAIL_START)}f;",
               f"ClearingInset = {lib.num(yard.CLEARING_INSET)}f;",
               "LooseJackKeyBase = 3_000_000, LooseStickKeyBase = 4_000_000;", f"DefaultSeed => {yard.DEFAULT_SEED};",
               "class SpawnableRiposteYard : SpawnableJackstraw", f"LayCapacity => {LAY_CAPACITY};"):
    if needle not in yard_cs + g.read("Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableJackstraw.cs"):
        errors.append(f"the yard's C# no longer declares '{needle}' - update riposte_yard_budget.py with it")
for field in YARD_FIELDS:
    if not re.search(r"\b" + field + r" = ", yard_cs):
        errors.append(f"SpawnableRiposteYard has no field {field}")
if "Hash01" not in yard_cs or "_r." in yard_cs or "Random.Range" in yard_cs or "intensityLevel" in yard_cs:
    errors.append("SpawnableRiposteYard must draw only from Hash01 (the same arena on every peer) and read no intensity")
if "Domains.Blue" not in yard_cs or re.search(r"new Color|Color\.", yard_cs):
    errors.append("the yard's prisms are neutral (Domains.Blue) and it names no colour of its own")


def yard_errors(r, laid):
    """The placement rules on one laid rung. Returns a list of what is wrong."""
    out, c, tag = [], laid["census"], f"I{r['i']}"
    R, w = r["heapRadius"], r["laneRadius"]
    ring = laid["clearings"][1:] if r["centreClearingRadius"] > 0 else laid["clearings"]
    if c["skippedSticks"] or c["skippedJacks"]:
        out.append(f"{tag}: {c['skippedSticks']} stick(s) and {c['skippedJacks']} jack(s) found no room")
    if c["minMargin"] < jb.MIN_MARGIN:
        out.append(f"{tag}: a piece sits {c['minMargin']:.6f} u from a rejection boundary (float32 could differ)")
    if c["prisms"] >= LAY_CAPACITY:
        out.append(f"{tag}: {c['prisms']} prisms do not fit the lay capacity {LAY_CAPACITY}")
    for q in laid["prisms"]:
        if yard.mag(q[0]) > R + jb.SEGMENT:
            out.append(f"{tag}: a {q[4]} prism lies outside the heap")
            break
    # Lanes: each ends in its own refuel clearing, and nothing but its gates stands in it.
    for k, (axis, end) in enumerate(laid["lanes"]):
        centre, radius = ring[k]
        if jb.point_segment_sqr(centre, yard.ORIGIN, end) > 1e-6:
            out.append(f"{tag}: lane {k} does not run through refuel clearing {k}")
        if yard.mag(centre) + radius > R:
            out.append(f"{tag}: lane {k}'s refuel clearing leaves the heap")
    for q in laid["prisms"]:
        if q[4] == "gate":
            continue
        for axis, end in laid["lanes"]:
            if jb.point_segment_sqr(q[0], yard.ORIGIN, end) < (w - jb.SEGMENT * 0.5) ** 2 and q[4] != "rail":
                out.append(f"{tag}: a loose {q[4]} prism stands inside a lane")
                break
    # Rails: parallel to their lane, just outside its wall, never inside a clearing.
    for k, a, b in laid["picketSegs"]:
        axis, end = laid["lanes"][k]
        d = jb.sub(b, a)
        if abs(jb.dot(jb.norm(d), axis) - 1.0) > 1e-6:
            out.append(f"{tag}: a picket of lane {k} is not parallel to it")
        off = (jb.point_segment_sqr(a, yard.ORIGIN, end)) ** 0.5
        if abs(off - (w + yard.RAIL_STANDOFF)) > 1e-3:
            out.append(f"{tag}: a picket of lane {k} runs {off:.2f} u from the axis, not {w + yard.RAIL_STANDOFF}")
        for centre, radius in laid["clearings"]:
            if jb.point_segment_sqr(centre, a, b) < radius * radius:
                out.append(f"{tag}: a picket of lane {k} enters a clearing")
    if r["railsPerLane"] and c["pickets"] < c["lanes"] * r["railsPerLane"]:
        out.append(f"{tag}: {c['pickets']} pickets cannot give every rail of every lane one")
    # Gates: on the lane's axis, between the centre clearing and the refuel.
    for k, centre in laid["gateCentres"]:
        axis, end = laid["lanes"][k]
        along = jb.dot(centre, axis)
        far = yard.mag(ring[k][0]) - ring[k][1]
        if jb.point_segment_sqr(centre, yard.ORIGIN, end) > 1e-6 or not r["centreClearingRadius"] < along < far:
            out.append(f"{tag}: a gate of lane {k} is not on its axis between the two clearings")
    want_gates = sum(1 for k in range(c["lanes"]) if yard.gated_lane(k, r["gateEveryNthLane"])) * r["gatesPerLane"]
    if c["gates"] != want_gates:
        out.append(f"{tag}: {c['gates']} gates laid, {want_gates} authored")
    # Thorns and the ironwood core are zones, not counts.
    for a, b, thorned, iron in laid["stickSegs"]:
        d = min(jb.segment_segment_sqr(a, b, yard.ORIGIN, end) ** 0.5 for _, end in laid["lanes"])
        mid = tuple((a[n] + b[n]) * 0.5 for n in range(3))
        if thorned != (r["thornBand"] > 0 and d < w + r["thornBand"]):
            out.append(f"{tag}: a stick {d:.1f} u from a lane has the wrong tips")
        if iron != (r["ironwoodCoreRadius"] > 0 and yard.mag(mid) < r["ironwoodCoreRadius"]):
            out.append(f"{tag}: a stick {yard.mag(mid):.1f} u from the centre has the wrong heart")
    shielded = c["gates"] * (1 + 4 * jb.JACK_ARM_PRISMS) + c["jacks"] + c["ironwoodSticks"] * (jb.IRONWOOD_TO - jb.IRONWOOD_FROM)
    if c["shielded"] != shielded or c["danger"] != 2 * c["thornedSticks"]:
        out.append(f"{tag}: the census does not add up (shielded {c['shielded']} vs {shielded}, danger {c['danger']})")
    return out


for r in RUNGS:
    errors += yard_errors(r, r["laid"])
# The ladder: I1 mostly helps, I4 mostly demands the read. Help falls and cost rises on every step.
CEN = [r["laid"]["census"] for r in RUNGS]
if CEN[0]["shielded"] or CEN[0]["danger"]:
    errors.append("I1 must carry no shielded or danger prism: it teaches the Charge hit")
if CEN[1]["danger"] or not CEN[1]["gates"]:
    errors.append("I2 adds the gates and nothing red")
if not CEN[2]["thornedSticks"] or CEN[2]["ironwoodSticks"] or not CEN[3]["ironwoodSticks"]:
    errors.append("red tips arrive at I3 and ironwood at I4")
for lo, hi, a, b in zip(RUNGS, RUNGS[1:], CEN, CEN[1:]):
    step = f"I{lo['i']} -> I{hi['i']}"
    if hi["laneRadius"] >= lo["laneRadius"] or hi["railsPerLane"] >= lo["railsPerLane"]:
        errors.append(f"{step}: the lanes must narrow and each lane lose a rail")
    if b["lanes"] / hi["ringClearingCount"] > a["lanes"] / lo["ringClearingCount"]:
        errors.append(f"{step}: the share of refuels a lane reaches must not rise")
    help_a, help_b = a["railPrisms"] / a["prisms"], b["railPrisms"] / b["prisms"]
    cost_a, cost_b = (a["shielded"] + a["danger"]) / a["prisms"], (b["shielded"] + b["danger"]) / b["prisms"]
    if help_b >= help_a or cost_b <= cost_a:
        errors.append(f"{step}: the fuel-rail share must fall ({help_a:.3f} -> {help_b:.3f}) and the shielded-and-"
                      f"danger share rise ({cost_a:.3f} -> {cost_b:.3f})")
    if b["prisms"] < a["prisms"] * 1.25:
        errors.append(f"{step}: under 25% more prisms")
# A rule that cannot fail proves nothing: each one is shown to fire on a broken arena.
_bad = dict(RUNGS[2], laneRadius=RUNGS[2]["laneRadius"])
_laid = yard.build(_bad)
_laid["picketSegs"] = [(k, jb.add(a, (9.0, 0.0, 0.0)), jb.add(b, (9.0, 0.0, 0.0))) for k, a, b in _laid["picketSegs"]]
_laid["gateCentres"] = [(k, jb.add(c, (0.0, 30.0, 0.0))) for k, c in _laid["gateCentres"]]
_laid["stickSegs"] = [(a, b, not t, not i) for a, b, t, i in _laid["stickSegs"]]
_fired = " ".join(yard_errors(_bad, _laid))
for word in ("from the axis", "is not on its axis", "wrong tips", "wrong heart"):
    if word not in _fired:
        errors.append(f"negative control: the placement check '{word}' did not fire on a broken arena")
if scene is not None:
    for i, gd in enumerate(CELLS):
        if scene.count(gd) != 1:
            errors.append(f"scene does not reference Riposte Cell Config {i + 1} exactly once")
    if any(gd in scene for gd in JACKSTRAW_CELLS):
        errors.append("scene still plays in Jackstraw's cells")
    if ring_block(RINGS) not in scene:
        errors.append("scene's spawn rings are not the Lists' (heap radius + 90)")

errors += htr.plain_scalar_errors("Description", DESCRIPTION)
errors += htr.yaml_parse_errors(g.files)
g.finish(errors, referenced={"Vessel": EXISTING["Vessel_Swordfish"]},
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()))
