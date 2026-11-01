#!/usr/bin/env python3
"""
Authors every serialized asset Lantern Run needs (GameModes.LanternRun, the id READ from GameModes.cs). The shared
work is hull_time_race.author; this file is the Angler's spec.

Run from the repo root:  python3 Tools/Build/author_lantern_run_assets.py [--check]

WHAT THIS MODE IS. The Angler's TIME card (Shombith, 2026-11-01: "as per the abilities of the angler it can paint at two
places where there is prism mass clusters and the fauna will go there to eat it and that way angler can zoom towards
where its fauna is and he has two of these ... so he can in a way paint its path and zoom towards it"). A lapped circuit
on the shared HeadlongCircuit solver where every leg carries TWO prism mass clusters, one for each of the Angler's two
groups (LanternRunCourse). The vessel is the shipped Angler - the mode lays the clusters and nothing else. See
Assets/_Scripts/Controller/Arcade/LANTERNRUN.md.

The Angler AI (AnglerLeapfrogAIPolicySO) and the throw fix (Fauna.LeashOwnsSpeed) are the HULL's, authored by
author_angler_assets.py.

THE DONOR IS THRESHER TIME'S SCENE. The clone swaps the controller script, the scoring rule, the detection clamp (a dash
moves the hull at up to ~2,000 u/s) and the Netcode ids, and adds the cluster prism (LanternRunController.clusterPrefab).
"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402

MODE_DIR = "Assets/_Scripts/Controller/Arcade/LanternRun"
# Assets/_Prefabs/Trails/SpawnablePrism.prefab (its Prism component is fileID 4563009547826722997): the pooled
# environment prism Serpentine's gate debris is laid from.
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

errors = []
# The words the controller posts at the start (GameToastSituation 158) must name what the mode asks for.
HINT = "Paint the two clusters on each leg, throw half your school at each, then dash after them through the ring."
if "clusters" not in HINT or "dash" not in HINT:
    errors.append("the start hint no longer teaches the loop (paint two clusters, dash)")

htr.author(dict(
    member="LanternRun", display="Lantern Run", hull="Angler", class_id=17,
    scripts={
        "LanternRunController":   f"{MODE_DIR}/LanternRunController.cs",
        "LanternRunCourse":       f"{MODE_DIR}/LanternRunCourse.cs",
        "LanternRunCourseSource": f"{MODE_DIR}/LanternRunCourseSource.cs",
        "LanternRunCourseTests":  "Assets/_Scripts/Tests/Editor/LanternRunCourseTests.cs",
    },
    doc="Assets/_Scripts/Controller/Arcade/LANTERNRUN.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameThresherTime.unity",
    donor_controller_guid=lib.guid("script/ThresherTimeController"),
    donor_rule_guid=lib.guid("asset/ThresherTimeScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/ThresherTimeScoringRule"), commit=135, lead=150, through=100, speed=4600),
    # The Angler cruises on a ~48 u circle (Thresher Time's AI numbers sit outside it). A dash carries the hull 75% of
    # a gap in 0.35 s: ~1,700 u/s on an 800 u gap, ~2,700 at the far end of a long leg. 6000 covers twice that.
    new_fields=FIELDS.format(rule="__RULE__", commit=135, lead=150, through=100, speed=6000),
    new_field_lines=(f"  clusterPrefab: {{fileID: 4563009547826722997, guid: {SPAWNABLE_PRISM}, type: 3}}\n"
                     "  nucleusMargin: 30\n"),
    max_speed=2700,
    target=16, laps=2, target_key="lanternRunGateTarget", target_after="brittleStarTimeGateTarget",
    # A quarter-of-race deficit (4 gates) x 0.45 = 1.8 element levels. The Angler's Time doubles its tamed fauna's
    # speed (x1 -> x2), so a throw - and the hull that dashes after it - goes faster: the buff lands on the race's axis.
    comeback=0.45,
    description="""Anglers only, and you paint your own path. Every leg holds two clusters of
    prism mass, one for each half of your school. Hold RT on the near one and let go to
    throw, paint the far one past the ring and throw again, then dash after each half
    and zoom straight through the ring. The clusters grow back. Two laps. Fastest
    domain home wins.""",
    preview_objective="Paint two clusters, throw both halves, dash through the ring",
    preview_notes="OPEN-ENDED, shell-only: Headlong''s solver cut for the Angler, laid as rings and mass clusters at match start, so a preview has no gates. It teaches the two-group leapfrog.",
    # LanternRun's own beat (LanternRunController posts it to the local pilot; GameToastSituation 158).
    extra_toasts=[
        (158, HINT, dict(domain_names=0)),
    ],
    extra_errors=errors,
    build_after="MinigameBrittleStarTime",
))
