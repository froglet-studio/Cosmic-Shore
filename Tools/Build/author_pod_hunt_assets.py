#!/usr/bin/env python3
"""
Authors every serialized asset the Pod Hunt game mode needs (GameModes.PodHunt = 99, hint toast 174).

Pod Hunt is the Orca's CHARGE game (the game kit roster, Docs/ElementalLoop/GAME_KIT_ROSTER.md row 9): a pod of
crewed hulls hunting each other through the Boneyard's wreckage. Every BLAST that reaches a rival hull - a gunner's
missile (direct, blast or shockwave) or the bomb's burst - is one point; the guns score nothing. The first DOMAIN to
the target wins, golf-timed. Omni crystals in the wreck reload every gunner seat.

NOTHING ABOUT THE ORCA CHANGES (the LOCKED vessel rule). The arena is the Boneyard's four cells, referenced (Salvo's
reason: a missile duel wants cover to fly round). The gunners aim and fire on their own (OrcaCrewAI); the mode's AI is
the helm alone (OrcaArcadeHelm, goal Hunt), which stands each AI hull off its cheapest rival. The scene is cloned
from Kelpwood's (the newest CombatPoints duel, whose controller declares settings / rule / arenaCell as this one does).

Idempotent and deterministic (arcade_mode_lib). Run from the repo root:
    python3 Tools/Build/author_pod_hunt_assets.py [--check]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import orca_games_common as orca  # noqa: E402

MODE_ID = 99
TOAST_HINT = 174
g = lib.Generator(MODE_ID, "PodHunt")
guid = lib.guid
AR = orca.AR

SCRIPT_PATHS = {
    "PodHuntController":       f"{AR}/PodHunt/PodHuntController.cs",
    "PodHuntScoringRuleSO":    f"{AR}/PodHunt/PodHuntScoringRuleSO.cs",
    "PodHuntBlastTurnMonitor": f"{AR}/TurnMonitors/PodHuntBlastTurnMonitor.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
G_SCRIPT.update(orca.script_guids())
G_ASSET = {
    "ArcadeGamePodHunt":      guid("asset/ArcadeGamePodHunt"),
    "PodHuntScoringRule":     guid("asset/PodHuntScoringRule"),
    "PodHuntSettings":        guid("asset/PodHuntSettings"),
    "GameToastConfigPodHunt": guid("asset/GameToastConfigPodHunt"),
    "ModePreviewPodHunt":     guid("asset/ModePreviewPodHunt"),
    "MinigamePodHunt.unity":  guid("asset/MinigamePodHunt.unity"),
}

# ── What it reads (never edits) ──────────────────────────────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
EXISTING["Vessel_Orca"] = orca.VESSEL_ORCA
# The Boneyard's four cells (Salvo's arena): no nucleus, wreckage out to ~520 u.
CELLS = ["cc21823da49bd38535c54796c4a1c168", "64160342670283d2a0c5088f1951295c",
         "7dc0173b8f31a743221e8f0bd3ec2d40", "5511269da8da60affaf693e51b8a60a1"]
for i, gd in enumerate(CELLS, 1):
    EXISTING[f"BoneyardCellConfig{i}"] = gd
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameKelpwood.unity"
DONOR = {
    "KelpwoodController":      guid("script/KelpwoodController"),
    "KelpwoodBiteTurnMonitor": guid("script/KelpwoodBiteTurnMonitor"),
    "KelpwoodSettings":        guid("asset/KelpwoodSettings"),
    "KelpwoodScoringRule":     guid("asset/KelpwoodScoringRule"),
}
DONOR_CELLS = [guid(f"asset/KelpwoodCellConfig{i}") for i in (1, 2, 3, 4)]
ANGLER_ID = lib.VESSEL_CLASS_ID["Angler"]

# ── The hunt ─────────────────────────────────────────────────────────────────
BLAST_TARGET = 15         # first cut, unplayed: kept in sync with EndConditionOverridesSO.DefaultPodHuntBlastTarget
BLAST_POINTS = 1
# Comeback - a FUNCTION OF THE TARGET (bonusLevels = deficit x rate). A quarter of the race behind (3.75 blasts) buys
# 1.1 element levels; Charge is the bomb's reach, so the comeback reaches the hunt.
COMEBACK_RATE = 0.3
# Salvo's ring on the same cells: no nucleus, so the floor carries the whole standoff - outside the ~520 u wreck,
# inside the AI's ~720 u missile reach of the far side.
SPAWN_RING = 700
OMNI_BALL = 420           # the omni crystals spawn in the wreck (Salvo's noNucleusSpawnRadius)
OMNI_LADDER = [4, 3, 3, 2]

CARD_REL = "Assets/_SO_Assets/Games/ArcadeGamePodHunt.asset"

# ── 1. .cs.meta for the scripts, and the folders / docs this mode adds ───────
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])
shared_minted = orca.emit_shared_metas(g)
g.folder_meta(f"{AR}/PodHunt")
g.text_meta(f"{AR}/PODHUNT.md")

# ── 2. Scoring rule ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Scoring Rules/PodHuntScoringRule.asset", G_ASSET["PodHuntScoringRule"],
             lib.header_for(G_SCRIPT["PodHuntScoringRuleSO"], "PodHuntScoringRule") +
             f"""  metric: 8
  golfRules: 1
  blastPoints: {BLAST_POINTS}
  gunHitPoints: 0
  blastsLeftTemplate: '{{count}} Blasts Left'
  blastsLandedTemplate: '{{count}} blasts'
  victoryHeader: VICTORY
  victoryLabel: HUNT TIME
  defeatHeader: DEFEAT
  defeatLabel: BLASTS LEFT
""")

# ── 3. Settings ──────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Games/PodHuntSettings.asset", G_ASSET["PodHuntSettings"],
             orca.settings_yaml("PodHuntSettings"))

# ── 4. Arcade card (2/2 is a RULE: a blast never scores on a team-mate, so a solo lobby has nobody to hunt) ──
DESCRIPTION = ("Orcas only - a pod hunt through the Boneyard's wreckage. Every blast that reaches a rival hull is a "
               "point: a gunner's missile, or the bomb's burst. The guns score nothing, but they break the cover a "
               "rival hides behind. Take an omni crystal to reload every gunner seat, and raise the danger shield "
               "when a missile comes for you. First team to the target wins.")
g.emit_asset(CARD_REL, G_ASSET["ArcadeGamePodHunt"],
             orca.card_yaml(EXISTING, "ArcadeGamePodHunt", MODE_ID, "Pod Hunt", DESCRIPTION, CARD_REL,
                            "MinigamePodHunt", golf=True, min_players=2, min_domains=2, comeback=COMEBACK_RATE))

# ── 5. Toasts ────────────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_PodHunt.asset", G_ASSET["GameToastConfigPodHunt"],
             lib.header_for(EXISTING["GameToastConfigSO"], "GameToastConfig_PodHunt") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(128, "{0} is a quarter of the way to {2} blasts", tint_domain=1, domain_names=0) +
             lib.toast(129, "{0} is halfway - {1}/{2} blasts", tint_domain=1, domain_names=0) +
             lib.toast(130, "{0} takes the lead - {1}/{2}", tint_domain=1, domain_names=0) +
             lib.toast(TOAST_HINT, "Missiles and the bomb score, the guns break cover. Take an omni crystal to "
                       "reload every seat", domain_names=0) +
             lib.toast(30, "Comeback system is on", domain_names=0, alpha=0.9))
g.register_toast_config(G_ASSET["GameToastConfigPodHunt"])

# ── 6. The scene's spawn ring and crystals ───────────────────────────────────
SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
         f"  spawnRingRadiusFloor: {SPAWN_RING}\n  spawnRingRadiusFloorByIntensity:\n"
         + "".join(f"  - {SPAWN_RING}\n" for _ in OMNI_LADDER) + "  spawnFormation: 0\n")
DONOR_SPAWN = ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
               "  spawnRingRadiusFloor: 368\n  spawnRingRadiusFloorByIntensity:\n"
               "  - 368\n  - 400\n  - 432\n  - 464\n  spawnFormation: 0\n")
OMNI = (f"  noNucleusSpawnRadius: {OMNI_BALL}\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
        "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
        + "".join(f"  - CrystalsPerPlayer: 0\n    ExtraCrystals: {n}\n" for n in OMNI_LADDER))
DONOR_OMNI = ("  noNucleusSpawnRadius: 200\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
              "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
              "  - CrystalsPerPlayer: 0\n    ExtraCrystals: 3\n  - CrystalsPerPlayer: 0\n    ExtraCrystals: 2\n"
              "  - CrystalsPerPlayer: 0\n    ExtraCrystals: 2\n  - CrystalsPerPlayer: 0\n    ExtraCrystals: 1\n")

# ── 7. Mode preview ──────────────────────────────────────────────────────────
g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_PodHunt.asset", G_ASSET["ModePreviewPodHunt"],
             lib.header_for(EXISTING["ModePreviewDefinitionSO"], "ModePreview_PodHunt") + f"""  Mode: {MODE_ID}
  Notes: 'The Boneyard at each intensity, Salvo''s arena. OPEN-ENDED - it scores on blasts that reach a
    rival hull and a solo preview has none; what it teaches is the wreck to fly round and the omni
    crystals that reload every seat. Vessel pinned to Orca({orca.ORCA_ID}).'
  PreviewCell: {{fileID: 11400000, guid: {CELLS[0]}, type: 2}}
  PreviewCellsByIntensity:
{orca.cell_list(CELLS)}  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  PreviewFauna: {{fileID: 0}}
  PreviewFaunaCount: 4
  Vessel: {orca.ORCA_ID}
  ObjectiveText: Fly a missile into a rival hull, and take an omni crystal to reload
  ObjectiveMetric: 8
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 1
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: {SPAWN_RING}
  SpawnRingRadiusFloorByIntensity:
""" + "".join(f"  - {SPAWN_RING}\n" for _ in OMNI_LADDER) + """  SpawnFormation: 0
  SpawnPoints: []
""")
g.register_preview(G_ASSET["ModePreviewPodHunt"])

# ── 8. Scene: Kelpwood's, with the identity, the cells, the AI hulls, the spawn ring and the crystals swapped ──
errors = []


def build_clone():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["KelpwoodBiteTurnMonitor"], G_SCRIPT["PodHuntBlastTurnMonitor"],
                          "turn monitor")
    scene = lib.swap_guid(scene, DONOR["KelpwoodController"], G_SCRIPT["PodHuntController"], "controller")
    scene = lib.swap_guid(scene, DONOR["KelpwoodSettings"], G_ASSET["PodHuntSettings"], "settings")
    scene = lib.swap_guid(scene, DONOR["KelpwoodScoringRule"], G_ASSET["PodHuntScoringRule"], "scoring rule")
    scene = lib.replace_block(scene, "  CellConfigs:\n" + orca.cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + orca.cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", "cell list")
    scene = orca.swap_ai_templates(scene, ANGLER_ID)
    scene = lib.replace_block(scene, DONOR_SPAWN, SPAWN, "spawn ring")
    scene = lib.replace_block(scene, DONOR_OMNI, OMNI, "omni crystals")
    return scene


AUTHORED_BLOCKS = ["  CellConfigs:\n" + orca.cell_list(CELLS) + "  cellTypeChoiceOptions: 1\n", SPAWN, OMNI]
SCENE_REL = f"{lib.SCENES_DIR}/MinigamePodHunt.unity"
scene, scene_errs = lib.committed_scene(SCENE_REL, build_clone, AUTHORED_BLOCKS)
errors += scene_errs
g.emit_scene("MinigamePodHunt", G_ASSET["MinigamePodHunt.unity"], scene)

# ── 9. Registries ────────────────────────────────────────────────────────────
g.register_arcade_card(G_ASSET["ArcadeGamePodHunt"])
g.register_always_unlocked()
g.register_build_scene("MinigameKelpwood", "MinigamePodHunt", G_ASSET["MinigamePodHunt.unity"])
g.set_end_condition("podHuntBlastTarget", after="glasshouseRoundSeconds", value=BLAST_TARGET)

# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
if 0.25 * BLAST_TARGET * COMEBACK_RATE < 1.0:
    errors.append(f"comeback rate {COMEBACK_RATE} is dead against target {BLAST_TARGET}: a quarter-of-target "
                  f"deficit buys {0.25 * BLAST_TARGET * COMEBACK_RATE:.2f} element levels (< 1)")
orca.validate_shared(g, errors)
orca.validate_enums(g, errors, "PodHunt", MODE_ID, "PodHuntHint", TOAST_HINT)
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
if f"DefaultPodHuntBlastTarget = {BLAST_TARGET};" not in eco:
    errors.append("EndConditionOverridesSO.DefaultPodHuntBlastTarget differs from BLAST_TARGET")
rule_cs = g.read(SCRIPT_PATHS["PodHuntScoringRuleSO"])
for needle in ("CombatHitClass.MissileShockwave => blastPoints", "CombatHitClass.MissileBlast     => blastPoints",
               "CombatHitClass.MissileDirect    => blastPoints", "_                               => gunHitPoints"):
    if needle not in rule_cs:
        errors.append(f"PodHuntScoringRuleSO no longer prices '{needle}' - a blast must be the only thing that scores")
hit_class = g.read("Assets/_Scripts/Data/Enums/CombatHitClass.cs")
for needle in ("MissileDirect = 1,", "MissileBlast = 3,", "MissileShockwave = 4,"):
    if needle not in hit_class:
        errors.append(f"CombatHitClass no longer has '{needle}'")
if SPAWN_RING <= 520 or SPAWN_RING >= 1200 - 300:
    errors.append(f"spawn ring {SPAWN_RING} must sit outside the ~520 u wreck and well inside the 1,200 u membrane")
if any(b > a for a, b in zip(OMNI_LADDER, OMNI_LADDER[1:])):
    errors.append(f"the omni crystal ladder must thin with intensity ({OMNI_LADDER})")

orca.validate_scene(errors, scene,
                    present={**{k: G_SCRIPT[k] for k in ("PodHuntController", "PodHuntBlastTurnMonitor")},
                             "PodHuntSettings": G_ASSET["PodHuntSettings"],
                             "PodHuntScoringRule": G_ASSET["PodHuntScoringRule"],
                             **{f"Boneyard cell {i}": gd for i, gd in enumerate(CELLS, 1)}},
                    gone={**DONOR, **{f"Kelpwood cell {i}": gd for i, gd in enumerate(DONOR_CELLS, 1)}},
                    donor_class=ANGLER_ID)

g.finish(errors, referenced=EXISTING,
         minted=[G_SCRIPT[k] for k in SCRIPT_PATHS] + list(G_ASSET.values()) + shared_minted
                + [guid(f"folder/{AR}/PodHunt"), guid(f"text/{AR}/PODHUNT.md")])
