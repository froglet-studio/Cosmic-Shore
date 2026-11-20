#!/usr/bin/env python3
"""
Authors every serialized asset the Trawl game mode needs (GameModes.Trawl = 118).

Trawl is the Jacks' SPACE game (Docs/ElementalLoop/GAME_KIT_ROSTER.md row 28; Garrett, 2026-11-15: "make all the
missing games you can"): a race to destroy hostile prisms with the net. While the jacks net (their flying mode),
everything that crosses the mouth between the two fish is caught and DRAGGED, and a dragged prism that runs into
another destroys both, scored to the net (JACKS.md §1.1; the prism-motion fundamental, Docs/PRISM_MOTION.md). The
netting hull rams nothing (IHullContactGate) and a shielded prism bounces instead of breaking, so the net is the only
way to score. The first DOMAIN to the target wins, golf-timed.

THE ARENA IS HARVEST'S (referenced, never forked): its field of sheaves, chaff and bales that never regrow, its
shielded cores from I2 and danger spokes and creatures at I4. Everything the net scores on is the unshielded hostile
part of it, so the generator asserts three domains' targets fit in the smallest rung's unshielded hostile pool
(harvest_budget.py, Harvest's own exact census). Harvest's team Space crystals are the Thresher's blast and are its
controller's, so Trawl does not lay them.

NOTHING ABOUT THE JACKS CHANGES (the LOCKED vessel rule). The AI skipper is the hull's race coach (IJacksRaceCoach)
that never offers a herding cycle, so an AI nets the whole match, and steers its autopilot through the densest
hostile stand.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_trawl_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import harvest_budget as field  # noqa: E402

MODE_ID = 118             # reserved by GAME_KIT_ROSTER.md row 28 - never "the next free id"
TOAST_HINT = 193          # reserved by the same row
g = lib.Generator(MODE_ID, "Trawl")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "TrawlController":       f"{AR}/Trawl/TrawlController.cs",
    "TrawlSettingsSO":       f"{AR}/Trawl/TrawlSettingsSO.cs",
    "TrawlScoringRuleSO":    f"{AR}/Trawl/TrawlScoringRuleSO.cs",
    "TrawlSkipper":          f"{AR}/Trawl/TrawlSkipper.cs",
    "TrawlPrismTurnMonitor": f"{AR}/TurnMonitors/TrawlPrismTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameTrawl":      guid("asset/ArcadeGameTrawl"),
    "TrawlScoringRule":     guid("asset/TrawlScoringRule"),
    "TrawlSettings":        guid("asset/TrawlSettings"),
    "GameToastConfigTrawl": guid("asset/GameToastConfigTrawl"),
    "ModePreviewTrawl":     guid("asset/ModePreviewTrawl"),
    "MinigameTrawl.unity":  guid("asset/MinigameTrawl.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Jacks"] = "8a41c6e2f0d94b7c9e35a1d7c4b8e2f6"            # SO_Class_Jacks.asset
JACKS_NET = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/JacksNet.cs"
JACKS_EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/JacksExecutor.cs"
PRISM_MOTION = "Assets/_Scripts/Controller/Prisms/PrismMotion.cs"
# The donor: Harvest's committed scene and cells, minted by author_harvest_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameHarvest.unity"
DONOR = {
    "HarvestController":       guid("script/HarvestController"),
    "HarvestPrismTurnMonitor": guid("script/HarvestPrismTurnMonitor"),
    "HarvestSettings":         guid("asset/HarvestSettings"),
    "HarvestScoringRule":      guid("asset/HarvestScoringRule"),
}
CELLS = [guid(f"asset/HarvestCellConfig{i}") for i in (1, 2, 3, 4)]
for i, gd in enumerate(CELLS, 1):
    EXISTING[f"Harvest Cell Config {i}"] = gd
THRESHER_ID = lib.VESSEL_CLASS_ID["Thresher"]
JACKS_ID = lib.VESSEL_CLASS_ID["Jacks"]

# ── The trawl ────────────────────────────────────────────────────────────────
PRISM_TARGET = 300        # an estimate, unplayed: kept in sync with EndConditionOverridesSO.DefaultTrawlPrismTarget
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (75 prisms) buys
# 2.25 element levels, Harvest's 2.25 at its own target.
COMEBACK_RATE = 0.03
RINGS = [r["spawnRing"] for r in field.RUNGS]   # Harvest's spawn ring, kept: the preview mirrors the scene's

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameTrawl.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Trawl")
g.text_meta(f"{AR}/TRAWL.md")

# ── 2. Scoring rule (Harvest's, inherited; Trawl's own words) ────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/TrawlScoringRule.asset", G_ASSET["TrawlScoringRule"],
             lib.header_for(G_SCRIPT["TrawlScoringRuleSO"], "TrawlScoringRule") +
             """  metric: 5
  golfRules: 1
  prismsLeftTemplate: '{count} Prisms Left'
  prismsDestroyedTemplate: '{count} Prisms'
  victoryHeader: VICTORY
  victoryLabel: TRAWL TIME
  defeatHeader: DEFEAT
  defeatLabel: PRISMS LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds": ([7, 6, 5, 4], "down"),
    "aiAimError":        ([70, 40, 18, 6], "down"),
}
SHARED = {"aiOvershoot": 160}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/TrawlSettings.asset", G_ASSET["TrawlSettings"],
             lib.header_for(G_SCRIPT["TrawlSettingsSO"], "TrawlSettings")
             + "".join(ladder_yaml(n) for n in LADDERS)
             + f"  aiOvershoot: {lib.num(SHARED['aiOvershoot'])}\n")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 1 / MinDomains 1: a race against the field (Harvest's), not a duel.
DESCRIPTION = ("Jacks only - trawl a field of sheaves with your net. Fly your two fish through the field and the net "
               "between them catches every loose prism it crosses and drags it along; a dragged prism that hits another "
               "breaks both, and every prism you break that is not your colour scores. Open the fish wide for a bigger "
               "net, and sweep the packed cores. Shielded prisms only bounce. Higher intensities shield the cores, paint "
               "the sheaves in team colours and add danger spokes and creatures. First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGameTrawl"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameTrawl") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Trawl
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameTrawl
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Jacks']}, type: 2}}
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
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Trawl.asset", G_ASSET["GameToastConfigTrawl"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Trawl") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(83, "<b>{0}</b> has trawled {1} prisms ({1}/{3})", tint_domain=1, domain_names=0,
                       every_n=60) +
             lib.toast(128, "{0} is a quarter of the way to {2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} prisms", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Fly the net through the sheaves - dragged prisms smash into each other and both "
                       "break", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigTrawl"])


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Trawl.asset", G_ASSET["ModePreviewTrawl"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Trawl") + f"""  Mode: {MODE_ID}
  Notes: 'Harvest''s field at each intensity - big neutral sheaves, then shielded cores, team colours and
    danger spokes. What it teaches is the sweep - fly the net through a packed stand so the prisms it
    drags smash into the rest. Vessel pinned to Jacks({JACKS_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {JACKS_ID}
  ObjectiveText: Fly the net through the sheaves - dragged prisms break what they hit
  ObjectiveMetric: 5
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {RINGS[0]}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {v}\n" for v in RINGS) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewTrawl"])

# ── 7. Scene: Harvest's, with the identity and the AI hulls swapped (its cells, spawn ring and crystals kept) ──
errors = []
CELL_BLOCK = "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n"


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["HarvestPrismTurnMonitor"], G_SCRIPT["TrawlPrismTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["HarvestController"], G_SCRIPT["TrawlController"], "controller")
    scene = lib.swap_guid(scene, DONOR["HarvestSettings"], G_ASSET["TrawlSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["HarvestScoringRule"], G_ASSET["TrawlScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {THRESHER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {JACKS_ID}\n    PlayerName: AI", "AI templates", expected=4)
    return scene


SCENE_REL = f"{lib.SCENES_DIR}/MinigameTrawl.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, [CELL_BLOCK])
errors += scene_errs
g.emit_scene("MinigameTrawl", G_ASSET["MinigameTrawl.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameTrawl"])
g.register_always_unlocked()
g.register_build_scene("MinigameHarvest", "MinigameTrawl", G_ASSET["MinigameTrawl.unity"])
g.set_end_condition("trawlPrismTarget", after="coronaBloomTarget", value=PRISM_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * PRISM_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {PRISM_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * PRISM_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The field never regrows and the net cannot break shielded mass: three domains' targets must fit in the smallest
# rung's unshielded hostile pool, for every playable colour (a sheaf in your colour is lit by your net, never moved).
for r in field.RUNGS:
    prisms = field.build(r)["prisms"]
    pools = {d: sum(1 for q in prisms if q[3] != "shielded" and q[4] != d) for d in field.PLAYABLE}
    if min(pools.values()) < 3 * PRISM_TARGET:
        errors.append(f"I{r['i']}: the smallest unshielded hostile pool ({min(pools.values())}) cannot hold three "
                      f"domains' targets ({3 * PRISM_TARGET}) - the field could run dry with nobody at the target")

# The load-bearing assumptions: the net drags prisms, a dragged prism that hits another annihilates both scored to the
# net's pilot, shielded mass bounces, and the coach seam the skipper uses exists.
net = g.read(JACKS_NET)
for needle in ("PrismMotion.Annihilate(", "PrismMotion.Bounces("):
    if needle not in net:
        errors.append(f"JacksNet no longer calls '{needle}' - Trawl scores the net's dragged prisms breaking each other; "
                      f"re-check how the net moves prisms")
motion = g.read(PRISM_MOTION)
if "struck.DamageByCollision(impact, domain, playerName" not in motion:
    errors.append("PrismMotion.Annihilate no longer credits the mover's pilot - Trawl would score nothing")
executor = g.read(JACKS_EXECUTOR)
for needle in ("public IJacksRaceCoach RaceCoach", "void StepAIRace()",
               "if (now < _swapReadyAt || !_coach.TryNextCycle(this, out _plan)) return;"):
    if needle not in executor:
        errors.append(f"JacksExecutor no longer has '{needle}' - the skipper keeps the AI netting by never answering a "
                      f"coach cycle; re-check the race-coach seam")
if f"TrawlHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.TrawlHint is not {TOAST_HINT}")
if f"Trawl = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Trawl is not {MODE_ID}")
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultTrawlPrismTarget = {PRISM_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultTrawlPrismTarget differs from PRISM_TARGET")

# The ladders: each monotone toward harder; C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["TrawlSettingsSO"])
for name, (values, harder) in LADDERS.items():
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"{name} = {init};" not in settings_cs:
        errors.append(f"{name}: the C# initializer does not match {init}")
for f_, value in SHARED.items():
    if f"{f_} = {lib.num(float(value))}f;" not in settings_cs:
        errors.append(f"TrawlSettingsSO.{f_} initializer differs from the authored {value}")

# The skipper reads the seat's difficulty, never the intensity, presses nothing, and never offers a herding cycle.
skipper = g.read(SCRIPT_PATHS["TrawlSkipper"])
if "SelectedIntensity" in skipper or "AIDifficultyFor(" not in skipper:
    errors.append("TrawlSkipper must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")
for banned in ("PerformShipControllerActions", "InputStatus", "Teleport(", "DropDecoy("):
    if banned in skipper:
        errors.append(f"TrawlSkipper calls '{banned}' - it may only steer the autopilot")
if "plan = default;\n            return false;" not in skipper:
    errors.append("TrawlSkipper.TryNextCycle must never offer a cycle (the AI nets the whole match)")

# The scene: every new identity present, every donor one gone, Harvest's four cells, four Jacks AIs, Harvest's ring.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("TrawlController", "TrawlPrismTurnMonitor")},
                     "TrawlSettings": G_ASSET["TrawlSettings"],
                     "TrawlScoringRule": G_ASSET["TrawlScoringRule"],
                     **{f"Harvest cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {JACKS_ID}\n") != 4 or f"  - vesselClass: {THRESHER_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Jacks AI templates")
    ring_block = (f"  spawnRingRadiusFloor: {RINGS[0]}\n  spawnRingRadiusFloorByIntensity:\n"
                  + "".join(f"  - {v}\n" for v in RINGS))
    if ring_block not in scene:
        errors.append("scene's spawn ring is no longer Harvest's - the preview mirrors it")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Trawl"), guid(f"text/{AR}/TRAWL.md")])
