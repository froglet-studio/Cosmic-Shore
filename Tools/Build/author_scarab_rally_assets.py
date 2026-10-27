#!/usr/bin/env python3
"""
Authors every serialized asset Scarab Rally needs (GameModes.ScarabRally, the id READ from GameModes.cs).
The shared work is hull_time_race.author; this file is the Scarab's spec.

Run from the repo root:  python3 Tools/Build/author_scarab_rally_assets.py [--check]

WHAT THIS MODE IS. The Scarab's TIME card: a ring race with no ball (Garrett, 2026-10-27: "a proper
ring race for scarab that doesn't rely on the ball at all but has the scarab racing around the map
using the throttle drift and juke mechanics"). Switchback's walk - an open chain of rings flown in
order, hairpins from I2, walls at I3, danger prisms at I4 - cut for the Scarab's throttle, drift
and juke by ScarabRallyCourse. Scarab Scramble stays the Scarab's ball game (now its Mass card).

THE DONOR IS SWITCHBACK'S SCENE (barren race cell, equatorial spawn ring, the I3/I4 features
prefab). The clone swaps the controller script (ScarabRallyController : SwitchbackController keeps
every serialized field the donor has), the scoring rule asset, the detection clamp, and the Netcode
ids; the card's Vessels list clamps every seat - AI included - to the Scarab.
"""
import os
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402

MODE_DIR = "Assets/_Scripts/Controller/Arcade/ScarabRally"
FEATURES = "  featuresPrefab: {{fileID: 5330000000000103, guid: 29ec5eb8fa7cb3fd1d816c63b5b39687, type: 3}}\n"
FIELDS = """  rule: {{fileID: 11400000, guid: {rule}, type: 2}}
  cellData: {{fileID: 11400000, guid: 8d4e8398eedc76c4dadb8604f89b9e1b, type: 2}}
  courseOuterRadius: 1080
  courseInnerRadiusFallback: 480
  innerRadiusNucleusFactor: 1.22
  firstGateDistance: 620
  gateBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: 260
  aiApproachLead: 300
  aiThroughDistance: 220
  aiCrystalDetourSlack: 220
  aiCrystalScanSeconds: 0.5
  maxPlausibleSpeed: {speed}
  reportResyncSeconds: 3
""" + FEATURES

htr.author(dict(
    member="ScarabRally", display="Scarab Rally", hull="Scarab", class_id=lib.VESSEL_CLASS_ID["Scarab"],
    scripts={
        "ScarabRallyController":   f"{MODE_DIR}/ScarabRallyController.cs",
        "ScarabRallyCourse":       f"{MODE_DIR}/ScarabRallyCourse.cs",
        "ScarabRallyCourseSource": f"{MODE_DIR}/ScarabRallyCourseSource.cs",
        "ScarabRallyCourseTests":  "Assets/_Scripts/Tests/Editor/ScarabRallyCourseTests.cs",
    },
    doc="Assets/_Scripts/Controller/Arcade/SCARABRALLY.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameSwitchback.unity",
    donor_controller_guid=lib.guid("script/SwitchbackController"),
    donor_rule_guid=lib.guid("asset/SwitchbackScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/SwitchbackScoringRule"), speed=400),
    # The detection clamp: the Scarab tops out at 324 u/s (Time 10) and its snap dash is a burst on top;
    # 1400 covers twice anything it flies, a ~47 u step at 60 fps - still under a respawn's jump.
    new_fields=FIELDS.format(rule="__RULE__", speed=1400),
    max_speed=600,
    # 24 rings, an OPEN chain flown once (laps 1).
    target=24, laps=1, target_key="scarabRallyGateTarget", target_after="wrasseRunGateTarget",
    # A quarter-of-race deficit (6 rings) x 0.4 = 2.4 element levels; the Scarab's Time raises its
    # throttle ceiling x1 -> x1.5, so the buff lands on the race's own axis.
    comeback=0.4,
    description="""Scarabs only, no ball - just you, your throttle and a course of rings.
    Hold the throttle to wind up speed, drift the hairpins to keep it, and juke into a
    mouth that sits off your line. Walls to weave and danger beside the fast line on
    the hard levels. First team to put a pilot through the last ring takes it.""",
    preview_objective="Hold the throttle, drift the hairpins, juke into the rings",
    preview_notes="OPEN-ENDED, shell-only: Switchback''s walk cut for the Scarab, laid as rings at match start, so a preview has no gates. It teaches the throttle, the drift and the juke.",
    build_after="MinigameSwitchback",
))
