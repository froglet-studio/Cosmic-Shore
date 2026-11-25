#!/usr/bin/env python3
"""
Authors every serialized asset the Stingherd game mode needs (GameModes.Stingherd = 117).

Stingherd is the Jacks' CHARGE game (Docs/ElementalLoop/GAME_KIT_ROSTER.md row 27; Garrett, 2026-11-15: "make all the
missing games you can"): herd your danger creatures into rival hulls. The Jacks' Charge ability is Danger Share - part
of the herd is dangerous, and a danger creature that touches a rival hull stings it (a Strike-class combat hit,
JacksExecutor.UpdateContacts) and is spent. Every sting is one point; the first DOMAIN to the target wins.

THE ARENA IS KELPWOOD'S (referenced, never forked): the 0G ball of kelp, its grazing tadpole herds - wild leashable
herbivores, so they join a herd that comes near them - and its omni crystals (a worm colony on the Jacks). The arcade
skill's rule: reference an existing cell where the mode wants what it authors.

THE LADDER is the herd's danger, through the card's StartingElements (a start level per intensity, the platform's
lever; Brittle Star Time's and Decoy Run's shape):
  I1 Charge 5 - half the herd is danger, and Attack is live: the herd goes for rivals by itself and every creature bites.
  I2 Charge 3, I3 Charge 1, I4 at rest (a quarter of the herd danger).
Kelpwood's own ladder thickens the kelp and thins the herds and crystals around it.

NOTHING ABOUT THE JACKS CHANGES (the LOCKED vessel rule). The AI drover is the hull's race coach (IJacksRaceCoach,
Decoy Run's seam): its plans run the jacks round the herd to drive it at a rival, in the controls a person uses.

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_stingherd_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import kelpwood_budget as kelp  # noqa: E402

MODE_ID = 117             # reserved by GAME_KIT_ROSTER.md row 27 - never "the next free id"
TOAST_HINT = 192          # reserved by the same row
g = lib.Generator(MODE_ID, "Stingherd")
guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SCRIPT_PATHS = {
    "StingherdController":        f"{AR}/Stingherd/StingherdController.cs",
    "StingherdSettingsSO":        f"{AR}/Stingherd/StingherdSettingsSO.cs",
    "StingherdScoringRuleSO":     f"{AR}/Stingherd/StingherdScoringRuleSO.cs",
    "StingherdDrover":            f"{AR}/Stingherd/StingherdDrover.cs",
    "StingherdStingTurnMonitor":  f"{AR}/TurnMonitors/StingherdStingTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_ASSET = {
    "ArcadeGameStingherd":      guid("asset/ArcadeGameStingherd"),
    "StingherdScoringRule":     guid("asset/StingherdScoringRule"),
    "StingherdSettings":        guid("asset/StingherdSettings"),
    "GameToastConfigStingherd": guid("asset/GameToastConfigStingherd"),
    "ModePreviewStingherd":     guid("asset/ModePreviewStingherd"),
    "MinigameStingherd.unity":  guid("asset/MinigameStingherd.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Jacks"] = "8a41c6e2f0d94b7c9e35a1d7c4b8e2f6"            # SO_Class_Jacks.asset
JACKS_EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/JacksExecutor.cs"
JACKS_PREFAB = "Assets/_Prefabs/Spacevessels/Jacks.prefab"   # the executor's onCombatHitLanded is wired here
# The donor: Kelpwood's committed scene and cells, minted by author_kelpwood_assets.py with the same guid function.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameKelpwood.unity"
DONOR = {
    "KelpwoodController":      guid("script/KelpwoodController"),
    "KelpwoodBiteTurnMonitor": guid("script/KelpwoodBiteTurnMonitor"),
    "KelpwoodSettings":        guid("asset/KelpwoodSettings"),
    "KelpwoodScoringRule":     guid("asset/KelpwoodScoringRule"),
}
CELLS = [guid(f"asset/KelpwoodCellConfig{i}") for i in (1, 2, 3, 4)]
for i, gd in enumerate(CELLS, 1):
    EXISTING[f"Kelpwood Cell Config {i}"] = gd
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]
JACKS_ID = lib.VESSEL_CLASS_ID["Jacks"]

# ── The sting race ───────────────────────────────────────────────────────────
STING_TARGET = 12         # first cut, unplayed: kept in sync with EndConditionOverridesSO.DefaultStingherdStingTarget
STING_POINTS = 1
STRIKE_CLASS = 5          # CombatHitClass.Strike - what JacksExecutor.Strike reports
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (3 stings) buys
# 1.2 element levels. On the Jacks, Charge is the danger share and Time the herd's speed: both move this fight.
COMEBACK_RATE = 0.4
# The herd's danger per intensity: the card's start Charge (a fraction of ten levels, the StartingElements unit;
# Brittle Star Time's 0.5 is "Time 5"). I1 starts at the Charge 5 upgrade (Attack); I4 at rest.
START_CHARGE = {1: 0.5, 2: 0.3, 3: 0.1}
RINGS = [r["spawnRing"] for r in kelp.RUNGS]   # Kelpwood's spawn ring, kept: the preview mirrors the scene's

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGameStingherd.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
g.folder_meta(f"{AR}/Stingherd")
g.text_meta(f"{AR}/STINGHERD.md")

# ── 2. Scoring rule (Jackstraw's, inherited; Stingherd's own words) ──────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/StingherdScoringRule.asset", G_ASSET["StingherdScoringRule"],
             lib.header_for(G_SCRIPT["StingherdScoringRuleSO"], "StingherdScoringRule") +
             f"""  metric: 8
  golfRules: 1
  strikePoints: {STING_POINTS}
  otherHitPoints: 0
  strikesLeftTemplate: '{{count}} Stings Left'
  strikesLandedTemplate: '{{count}} stings'
  victoryHeader: VICTORY
  victoryLabel: DRIVE TIME
  defeatHeader: DEFEAT
  defeatLabel: STINGS LEFT
""")

# ── 3. Mode settings (the ladders: index = AI difficulty - 1; Easy, Medium, Hard, Legendary) ──
LADDERS = {
    "aiRetargetSeconds": ([2, 1.5, 1, 0.6], "down"),
    "aiAimError":        ([120, 70, 35, 10], "down"),
    "aiStandOffRings":   ([0.9, 0.75, 0.6, 0.5], "down"),
    "aiFlankRings":      ([0.8, 0.7, 0.6, 0.5], "down"),
}
SHARED = {"aiHumanFocus": 2}


def ladder_yaml(name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in LADDERS[name][0])


g.emit_asset("Assets/_SO_Assets/Games/StingherdSettings.asset", G_ASSET["StingherdSettings"],
             lib.header_for(G_SCRIPT["StingherdSettingsSO"], "StingherdSettings")
             + "".join(ladder_yaml(n) for n in LADDERS)
             + f"  aiHumanFocus: {lib.num(SHARED['aiHumanFocus'])}\n")

# ── 4. Arcade card ───────────────────────────────────────────────────────────
# MinPlayers 2 / MinDomains 2 is a RULE: a sting never takes a team-mate, so a solo lobby has nobody to sting.
DESCRIPTION = ("Jacks only - herd your danger creatures into rival hulls. Part of your herd is dangerous; run your jacks "
               "behind it and it swims away from them, toward whoever you want stung. A danger creature that touches a "
               "rival stings it - one point - and is spent, so keep your herd fed and big. The tadpoles grazing the kelp "
               "join a herd that comes near them. On the easy levels half your herd is dangerous and it goes for rivals by "
               "itself; on the hardest only a quarter, in the thickest kelp. First team to the target wins.")


def starting_elements():
    rows = ""
    for i, charge in sorted(START_CHARGE.items()):
        rows += (f"  - Class: {JACKS_ID}\n    Intensity: {i}\n    Levels:\n      Mass: 0\n"
                 f"      Charge: {lib.num(charge)}\n      Space: 0\n      Time: 0\n")
    return "  StartingElements:\n" + rows


g.emit_asset(CARD_REL, G_ASSET["ArcadeGameStingherd"],
             lib.header_for(EXISTING["SO_ArcadeGame"], "ArcadeGameStingherd") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Stingherd
  Description: {lib.wrap_yaml_scalar(DESCRIPTION)}
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {lib.card_background(CARD_REL)}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameStingherd
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Jacks']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 9
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  ViewUserAction: 0
  PlayUserAction: 0
{starting_elements()}  ComebackRatePerScoreDeficit: {lib.num(COMEBACK_RATE)}
""")

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Stingherd.asset", G_ASSET["GameToastConfigStingherd"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_Stingherd") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(86, "<b>{0}</b> stung a rival - the herd struck!", tint_domain=1, domain_names=0) +
             lib.toast(128, "{0} is a quarter of the way to {2} stings", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} stings", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Run your jacks behind your herd to drive it into a rival - its danger creatures "
                       "sting", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigStingherd"])


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


# ── 6. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Stingherd.asset", G_ASSET["ModePreviewStingherd"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Stingherd") + f"""  Mode: {MODE_ID}
  Notes: 'Kelpwood''s kelp ball at each intensity - more strands, fewer tadpole herds. OPEN-ENDED - it scores on
    stinging rival hulls and a solo preview has none; what it teaches is the kelp and the herds to drive.
    Vessel pinned to Jacks({JACKS_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {JACKS_ID}
  ObjectiveText: Drive your herd into a rival hull - its danger creatures sting
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {RINGS[0]}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {v}\n" for v in RINGS) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewStingherd"])

# ── 7. Scene: Kelpwood's, with the identity and the AI hulls swapped (its cells, spawn ring and crystals kept) ──
errors = []
CELL_BLOCK = "  CellConfigs:\n" + cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n"


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["KelpwoodBiteTurnMonitor"], G_SCRIPT["StingherdStingTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["KelpwoodController"], G_SCRIPT["StingherdController"], "controller")
    scene = lib.swap_guid(scene, DONOR["KelpwoodSettings"], G_ASSET["StingherdSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["KelpwoodScoringRule"], G_ASSET["StingherdScoringRule"], "scoring rule")
    scene = lib.swap_guid(scene, f"  - vesselClass: {ANGLER_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {JACKS_ID}\n    PlayerName: AI", "AI templates", expected=4)
    return scene


SCENE_REL = f"{lib.SCENES_DIR}/MinigameStingherd.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, [CELL_BLOCK])
errors += scene_errs
g.emit_scene("MinigameStingherd", G_ASSET["MinigameStingherd.unity"], scene)

# ── 8. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGameStingherd"])
g.register_always_unlocked()
g.register_build_scene("MinigameKelpwood", "MinigameStingherd", G_ASSET["MinigameStingherd.unity"])
g.set_end_condition("stingherdStingTarget", after="harvestPrismTarget", value=STING_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * STING_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {STING_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * STING_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")

# The load-bearing assumptions: a danger creature still stings a rival hull as a Strike admitted through TryAdmit and
# raised on the combat channel, the herd's danger is still the Charge share, and the coach seam the drover uses exists.
executor = g.read(JACKS_EXECUTOR)
for needle in ("bool danger = m.Roll < share;",
               "CombatHitDrain.TryAdmit(victim, _status, CombatHitClass.Strike, config.StrikeCooldown,",
               "HitClass = CombatHitClass.Strike", "public IJacksRaceCoach RaceCoach", "public bool RaceCycleBusy",
               "public Vector3 RaceBoardUp", "internal Vector3 HerdCentre", "internal float HerdSpread",
               "internal Vector3 JacksCentre", "public float Ring =>", "public int HerdCount",
               "bool ChargeUpgraded"):
    if needle not in executor:
        errors.append(f"JacksExecutor no longer has '{needle}' - Stingherd's rule pays a sting and its drover coaches "
                      f"the herd through it; re-check how the Jacks sting and how a coach drives them")
if "onCombatHitLanded: {fileID: 11400000, guid: 45fb43a0cddb5cc3cc4dc448df994152" not in g.read(JACKS_PREFAB):
    errors.append("Jacks.prefab's JacksExecutor.onCombatHitLanded is no longer the game's combat-hit channel - stings "
                  "would score nothing")
if not g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs").count(f"Strike = {STRIKE_CLASS},"):
    errors.append(f"CombatHitClass.Strike is no longer {STRIKE_CLASS}")
if f"StingherdHint = {TOAST_HINT}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
    errors.append(f"GameToastSituation.StingherdHint is not {TOAST_HINT}")
if f"Stingherd = {MODE_ID}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
    errors.append(f"GameModes.Stingherd is not {MODE_ID}")

eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultStingherdStingTarget = {STING_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultStingherdStingTarget differs from STING_TARGET")

# The danger ladder runs down with intensity, and I1 starts at the Charge 5 upgrade.
charges = [START_CHARGE.get(i, 0.0) for i in (1, 2, 3, 4)]
if any(b > a for a, b in zip(charges, charges[1:])):
    errors.append(f"the start Charge must fall with intensity ({charges})")
if charges[0] < 0.5:
    errors.append("I1 must start at Charge 5 (0.5) so Attack is live on the intro rung")

# The ladders: each monotone toward harder; C# initializers agree.
settings_cs = g.read(SCRIPT_PATHS["StingherdSettingsSO"])
for name, (values, harder) in LADDERS.items():
    if len(values) != 4:
        errors.append(f"{name}: needs four rungs")
    pairs = list(zip(values, values[1:]))
    if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
        errors.append(f"{name}: not monotone toward harder ({values})")
    init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
    if f"{name} = {init};" not in settings_cs:
        errors.append(f"{name}: the C# initializer does not match {init}")
for field, value in SHARED.items():
    if f"{field} = {lib.num(float(value))}f;" not in settings_cs:
        errors.append(f"StingherdSettingsSO.{field} initializer differs from the authored {value}")

# The drover reads the seat's difficulty, never the intensity, presses nothing, and lays and flies to no decoy.
drover = g.read(SCRIPT_PATHS["StingherdDrover"])
if "SelectedIntensity" in drover or "AIDifficultyFor(" not in drover:
    errors.append("StingherdDrover must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")
for banned in ("PerformShipControllerActions", "InputStatus", "Teleport(", "DropDecoy("):
    if banned in drover:
        errors.append(f"StingherdDrover calls '{banned}' - it may only answer coach plans and steer the autopilot")
if "Jack0Lays = -1, Jack1Lays = -1, FirstTravel = -1, SecondTravel = -1" not in drover:
    errors.append("StingherdDrover's plan must lay no decoy and travel nowhere (a herd does not follow a flying hull)")

# The spawn ring: Kelpwood's, inside the cell.
for i, r in enumerate(RINGS, 1):
    if not (200 <= r <= 900):
        errors.append(f"I{i}: spawn ring {r} is outside the cell's playable shell")

# The scene: every new identity present, every donor one gone, Kelpwood's four cells, four Jacks AIs.
if scene is not None:
    for name, gd in {**{k: G_SCRIPT[k] for k in ("StingherdController", "StingherdStingTurnMonitor")},
                     "StingherdSettings": G_ASSET["StingherdSettings"],
                     "StingherdScoringRule": G_ASSET["StingherdScoringRule"],
                     **{f"Kelpwood cell {i}": gd for i, gd in enumerate(CELLS, 1)}}.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in DONOR.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {JACKS_ID}\n") != 4 or f"  - vesselClass: {ANGLER_ID}\n" in scene:
        errors.append("scene does not carry exactly 4 Jacks AI templates")
    ring_block = (f"  spawnRingRadiusFloor: {RINGS[0]}\n  spawnRingRadiusFloorByIntensity:\n"
                  + "".join(f"  - {v}\n" for v in RINGS))
    if ring_block not in scene:
        errors.append("scene's spawn ring is no longer Kelpwood's - the preview mirrors it")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values())
                + [guid(f"folder/{AR}/Stingherd"), guid(f"text/{AR}/STINGHERD.md")])
