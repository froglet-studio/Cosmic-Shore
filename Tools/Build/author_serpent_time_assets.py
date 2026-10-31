#!/usr/bin/env python3
"""
Authors every serialized asset Serpent Time needs (GameModes.SerpentTime, the id READ from GameModes.cs).
The shared work is hull_time_race.author; this file is the Serpent's spec.

Run from the repo root:  python3 Tools/Build/author_serpent_time_assets.py [--check]

WHAT THIS MODE IS. The Serpent's TIME card (it had none: Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md §2), reshaped
2026-10-27 into the DIRTY race (Docs/Studios/SERPENT_TIME.md): ring clusters,
a shape closing every ring (a core to break, gaps to slip through), gate debris that grows back, an AI that walls
and shoots. The rules are SerpentTimeController's; this file authors
the card text and the mode's own toasts.
Thresher Time's shape - a lapped circuit on the shared HeadlongCircuit solver - cut by SerpentTimeCourse
against the Serpent's Solid Fuel Pellets: four in the tank, each a 3 s burn that stacks (160 u/s on
one, 460 on four), one back every ~3.6 s. See Assets/_Scripts/Controller/Arcade/SERPENTTIME.md.

THE DONOR IS THRESHER TIME'S SCENE (barren race cell, equatorial spawn ring, a `laps` field). The clone
swaps the controller script, the scoring rule asset, the AI approach, the detection clamp and the
Netcode ids; the card's Vessels list clamps every seat to the Serpent.
"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402

MODE_DIR = "Assets/_Scripts/Controller/Arcade/SerpentTime"
# Assets/_Prefabs/Trails/SpawnablePrism.prefab (its Prism component is fileID 4563009547826722997).
SPAWNABLE_PRISM = lib.existing_guid("Assets/_Prefabs/Trails/SpawnablePrism.prefab")
FIELDS = """  rule: {{fileID: 11400000, guid: {rule}, type: 2}}
  cellData: {{fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  laps: 2
  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: {commit}
  aiApproachLead: {lead}
  aiThroughDistance: {through}
  aiCrystalDetourSlack: 220
  aiCrystalScanSeconds: 0.5
  maxPlausibleSpeed: {speed}
  reportResyncSeconds: 3
"""

# The race's OWN cells, one per intensity, so each intensity is a different place (Shombith, 2026-10-31: "we need the
# intensity level to feel like different experiences in different environments ... maybe add back fauna"; picked the
# Shallows > Grazing Grounds > Hunting Grounds > The Deep ladder and "medium, up to ~25" creatures). Every cell is Skim
# Race's barren cell and spawn profile copied with only names, the cross-reference and the fauna list changed; each
# creature is an existing config copied with only its name and its numbers changed (Skim Race's tadpole and brittlestar,
# Garland's shark, Scarab Scramble's piranha), so no other mode's cell or creature is touched. The tint each place wears
# is the controller's (SerpentTimeController.worldTintByIntensity). Re-read from the sources every run.
SKIM_DIR = "Assets/_SO_Assets/Cell Configs/Skim Race Cell"
OWN_DIR = "Assets/_SO_Assets/Cell Configs/Serpentine Cell"
FAUNA_SOURCES = {
    "Tadpole": f"{SKIM_DIR}/Skim Race Tadpole Fauna Config Data.asset",
    "Brittlestar": f"{SKIM_DIR}/Skim Race Brittlestar Fauna Config Data.asset",
    "Shark": "Assets/_SO_Assets/Cell Configs/Garland Cell/Garland Shark Fauna Config Data.asset",
    "Piranha": "Assets/_SO_Assets/Cell Configs/Scarab Scramble Cell/Scarab Scramble Piranha Fauna Config Data.asset",
}
# intensity: (place, [(creature, InitialSpawnCount, PopulationSize, MaxLivePopulation), ...]); live caps sum to <= 25.
WORLDS = {
    1: ("Shallows", [("Tadpole", 6, 6, 10)]),
    2: ("Grazing Grounds", [("Tadpole", 8, 8, 14), ("Brittlestar", 3, 3, 6)]),
    3: ("Hunting Grounds", [("Shark", 2, 2, 3), ("Piranha", 6, 6, 10), ("Tadpole", 6, 6, 10)]),
    4: ("The Deep", [("Shark", 2, 2, 3), ("Piranha", 8, 8, 12), ("Brittlestar", 2, 2, 4), ("Tadpole", 4, 4, 6)]),
}


def _read(rel):
    return open(os.path.join(lib.ROOT, rel), encoding="utf-8").read()


def _own_cell_assets():
    import re
    out, cells = [], []
    for intensity, (place, fauna) in WORLDS.items():
        assert sum(cap for _, _, _, cap in fauna) <= 25, f"I{intensity}: more than 25 live creatures"
        fauna_guids = []
        for creature, initial, population, cap in fauna:
            src = FAUNA_SOURCES[creature]
            text = _read(src)
            for key, value in (("InitialSpawnCount", initial), ("PopulationSize", population), ("MaxLivePopulation", cap)):
                text, n = re.subn(rf"^  {key}: \d+$", f"  {key}: {value}", text, flags=re.M)
                assert n == 1, f"{src}: {key} not found once"
            name = f"Serpentine I{intensity} {creature} Fauna Config Data"
            text = re.sub(r"^  m_Name: .*$", f"  m_Name: {name}", text, count=1, flags=re.M)
            g = lib.guid(f"asset/{name}")
            fauna_guids.append(g)
            out.append((f"{OWN_DIR}/{name}.asset", g, text))

        profile = _read(f"{SKIM_DIR}/Skim Race Spawn Profile.asset")
        listing = "  SupportedFaunas:\n" + "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in fauna_guids)
        profile, n = re.subn(r"^  SupportedFaunas:(?: \[\])?\n(?:  - \{[^\n]*\}\n)*", listing, profile, flags=re.M)
        assert n == 1, "spawn profile: SupportedFaunas not found once"
        pname = f"Serpentine I{intensity} Spawn Profile"
        profile = profile.replace("m_Name: Skim Race Spawn Profile\n", f"m_Name: {pname}\n")
        g_profile = lib.guid(f"asset/{pname}")
        out.append((f"{OWN_DIR}/{pname}.asset", g_profile, profile))

        cell = _read(f"{SKIM_DIR}/Skim Race Cell Config.asset")
        old_profile = lib.existing_guid(f"{SKIM_DIR}/Skim Race Spawn Profile.asset")
        assert cell.count(old_profile) == 1, "cell config: spawn profile not referenced once"
        cell = cell.replace(old_profile, g_profile)
        cname = f"Serpentine Cell Config {intensity}"
        cell = cell.replace("m_Name: Skim Race Cell Config\n", f"m_Name: {cname}\n")
        cell = re.sub(r"^  CellName: .*$", f"  CellName: Serpentine - {place}", cell, count=1, flags=re.M)
        cell = re.sub(r"^  Description: .*$", f"  Description: Serpentine at intensity {intensity} - {place}",
                      cell, count=1, flags=re.M)
        g_cell = lib.guid(f"asset/{cname}")
        out.append((f"{OWN_DIR}/{cname}.asset", g_cell, cell))
        cells.append(g_cell)
    return out, cells


OWN_CELL_ASSETS, OWN_CELLS = _own_cell_assets()

htr.author(dict(
    member="SerpentTime", display="Serpentine", hull="Serpent", class_id=lib.VESSEL_CLASS_ID["Serpent"],
    scripts={
        "SerpentTimeController":   f"{MODE_DIR}/SerpentTimeController.cs",
        "SerpentTimeCourse":       f"{MODE_DIR}/SerpentTimeCourse.cs",
        "SerpentTimeCourseSource": f"{MODE_DIR}/SerpentTimeCourseSource.cs",
        "SerpentTimeCourseTests":  "Assets/_Scripts/Tests/Editor/SerpentTimeCourseTests.cs",
        "SerpentTimeCluster":      f"{MODE_DIR}/SerpentTimeCluster.cs",
        "SerpentTimeClusterTests": "Assets/_Scripts/Tests/Editor/SerpentTimeClusterTests.cs",
        "SerpentineGate":          f"{MODE_DIR}/SerpentineGate.cs",
        "SerpentineGateTests":     "Assets/_Scripts/Tests/Editor/SerpentineGateTests.cs",
    },
    doc="Assets/_Scripts/Controller/Arcade/SERPENTTIME.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameThresherTime.unity",
    donor_controller_guid=lib.guid("script/ThresherTimeController"),
    donor_rule_guid=lib.guid("asset/ThresherTimeScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/ThresherTimeScoringRule"), commit=135, lead=150, through=100, speed=4600),
    # The AI burns pellets (ConsumeBoostAction's AI path), so it arrives at up to 460 u/s on a ~100 u
    # circle: the approach sits outside 2x that circle, Switchback's numbers. The clamp covers twice 460.
    new_fields=FIELDS.format(rule="__RULE__", commit=260, lead=300, through=220, speed=1400),
    # The gate debris (SerpentTimeController.debrisPrefab): the environment prism every arena lays.
    new_field_lines=f"  debrisPrefab: {{fileID: 4563009547826722997, guid: {SPAWNABLE_PRISM}, type: 3}}\n",
    max_speed=460,
    # The barren Skim Race cell with its trail-grazing food web (Skim Race's own, the fastest arena in the game). Rampage's
    # four forests ran here for a day (2026-10-31) and cost too much frame time; the shapes at the rings are mode-laid prisms (SerpentineGate).
    cell_configs=OWN_CELLS,
    extra_assets=OWN_CELL_ASSETS,
    target=16, laps=2, target_key="serpentTimeGateTarget", target_after="scarabRallyGateTarget",
    # A quarter-of-race deficit (4 gates) x 0.45 = 1.8 element levels - Thresher Time's number on the
    # same 16-gate race. The Serpent's Time lengthens a pellet's burn (3 s -> 4.8 s).
    comeback=0.45,
    description="""Serpents only, and it is a dirty race through a living cell. A great shape
    closes every ring - coiled at first, then fanged, then an iris - and its weave burns
    a careless hull. Slip out through a gap, or shoot or ram its gold core and the whole
    shape comes apart. Cloak just past a ring and a Seed Wall grows in the way of whoever
    is behind you. Two laps. Fastest domain home wins.""",
    preview_objective="Thread any ring - wall the gate behind you",
    preview_notes="OPEN-ENDED, shell-only: Headlong''s solver cut for the Serpent, laid as ring clusters at match start, so a preview has no gates. It teaches the fuel pellets.",
    # Serpent Time's own beat (SerpentTimeController posts it to the local pilot; GameToastSituation 156).
    extra_toasts=[
        (156, "A shape closes every ring. Slip through a gap, or break its gold core and it all comes apart.", dict(domain_names=0)),
    ],
    build_after="MinigameScarabRally",
))
