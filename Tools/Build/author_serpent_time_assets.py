#!/usr/bin/env python3
"""
Authors every serialized asset Serpent Time needs (GameModes.SerpentTime, the id READ from GameModes.cs).
The shared work is hull_time_race.author; this file is the Serpent's spec.

Run from the repo root:  python3 Tools/Build/author_serpent_time_assets.py [--check]

WHAT THIS MODE IS. The Serpent's TIME card (it had none: Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md §2), reshaped
2026-10-27 into the DIRTY race (Docs/Studios/SERPENT_TIME.md): ring clusters,
rings grown over with danger prisms, Rampage's living cells, gate debris that grows back, an AI that walls
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

htr.author(dict(
    member="SerpentTime", display="Serpent Time", hull="Serpent", class_id=lib.VESSEL_CLASS_ID["Serpent"],
    scripts={
        "SerpentTimeController":   f"{MODE_DIR}/SerpentTimeController.cs",
        "SerpentTimeCourse":       f"{MODE_DIR}/SerpentTimeCourse.cs",
        "SerpentTimeCourseSource": f"{MODE_DIR}/SerpentTimeCourseSource.cs",
        "SerpentTimeCourseTests":  "Assets/_Scripts/Tests/Editor/SerpentTimeCourseTests.cs",
        "SerpentTimeCluster":      f"{MODE_DIR}/SerpentTimeCluster.cs",
        "SerpentTimeClusterTests": "Assets/_Scripts/Tests/Editor/SerpentTimeClusterTests.cs",
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
    # The race runs in Rampage's four cells AS THEY SHIP (read-only: flora, fauna, nuclei and phase ladders are
    # Rampage's, Tools/Build/rampage_intensity.py; Bends, Sirocco and Bloomrush share them the same way). REVERSED:
    # Rampage's I1 is its densest forest (335 plants, ~59.6k prisms - easy to hit for a demolition mode) and its I4 the
    # sparsest (67 plants, ~11.9k). In a race the forest is the obstacle (Mass plants are shielded and bounce a hull,
    # Charge plants are danger), so Serpent I1 flies Rampage's sparse I4 cell and Serpent I4 its dense I1 cell.
    # The course solver keeps every gate inside the shell whatever the nucleus (200 at I1 -> 500 at I4).
    # List order = intensity 1-4.
    cell_configs=[lib.existing_guid(f"Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config {i}.asset")
                  for i in (4, 3, 2, 1)],
    target=16, laps=2, target_key="serpentTimeGateTarget", target_after="scarabRallyGateTarget",
    # A quarter-of-race deficit (4 gates) x 0.45 = 1.8 element levels - Thresher Time's number on the
    # same 16-gate race. The Serpent's Time lengthens a pellet's burn (3 s -> 4.8 s).
    comeback=0.45,
    description="""Serpents only, and it is a dirty race through a living cell. Every gate is a
    cluster of rings - thread any one - and some are grown over with danger prisms: shoot
    a way through, ram it and pay, or find the open ring. Cloak just past a ring and a
    Seed Wall grows in the way of whoever is behind you. Two laps. Fastest domain home
    wins.""",
    preview_objective="Thread any ring - wall the gate behind you",
    preview_notes="OPEN-ENDED, shell-only: Headlong''s solver cut for the Serpent, laid as ring clusters at match start, so a preview has no gates. It teaches the fuel pellets.",
    # Serpent Time's own beat (SerpentTimeController posts it to the local pilot; GameToastSituation 156).
    extra_toasts=[
        (156, "Any ring counts. Shoot through a covered ring, or find the open one.", dict(domain_names=0)),
    ],
    build_after="MinigameScarabRally",
))
