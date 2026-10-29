#!/usr/bin/env python3
"""
Authors every serialized asset Lantern Run needs (GameModes.LanternRun, the id READ from GameModes.cs). The shared
work is hull_time_race.author; this file is the Angler's spec plus the lanterns prefab.

Run from the repo root:  python3 Tools/Build/author_lantern_run_assets.py [--check]

WHAT THIS MODE IS. The Angler's TIME card (Garrett, 2026-10-29: "angler seems like a fun one. lets make a great angler
time game"). A lapped circuit on the shared HeadlongCircuit solver where the Angler travels by its school - paint the
lantern past the next ring, throw a group at it, dash after them through the mouth (ANGLER.md's leapfrog). See
Assets/_Scripts/Controller/Arcade/LANTERNRUN.md.

THE DONOR IS THRESHER TIME'S SCENE. The clone swaps the controller script (LanternRunController keeps the donor's
`laps` and adds `lanternsPrefab`), the scoring rule, the AI approach, the detection clamp (a dash moves the hull at up to
~2,000 u/s) and the Netcode ids. The lanterns prefab is Switchback's features prefab's shape on its own spawnable.
The AI's leapfrog (AnglerLeapfrogAIPolicySO) is on the HULL and authored by author_angler_assets.py.
"""
import os
import re
import sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
import hull_time_race as htr  # noqa: E402

MODE_DIR = "Assets/_Scripts/Controller/Arcade/LanternRun"
LANTERNS_SCRIPT = "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableLanternRunLanterns.cs"
LANTERNS_PREFAB = "Assets/_Prefabs/Spawnables/SpawnableLanternRunLanterns.prefab"
G_LANTERNS_SCRIPT = lib.guid("script/SpawnableLanternRunLanterns")
G_LANTERNS_PREFAB = lib.guid("asset/SpawnableLanternRunLanterns.prefab")
# The environment prism Switchback's features and Breakwater's stations lay.
ENVIRONMENT_PRISM, PRISM_FILEID = "ed9defc56162b4b4588e61c20984b6d9", 4563009547826722997
PF_GO, PF_TR, PF_MB = 7550000000000101, 7550000000000102, 7550000000000103

LANTERNS_PREFAB_TEXT = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{PF_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {PF_TR}}}
  - component: {{fileID: {PF_MB}}}
  m_Layer: 0
  m_Name: SpawnableLanternRunLanterns
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{PF_TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {PF_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{PF_MB}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {PF_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_LANTERNS_SCRIPT}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: 75
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: 1
  prism: {{fileID: {PRISM_FILEID}, guid: {ENVIRONMENT_PRISM}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
"""
PREFAB_META = (f"fileFormatVersion: 2\nguid: {G_LANTERNS_PREFAB}\nPrefabImporter:\n  externalObjects: {{}}\n"
               f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")

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
{extra}"""
LANTERNS_FIELD = f"  lanternsPrefab: {{{{fileID: {PF_MB}, guid: {G_LANTERNS_PREFAB}, type: 3}}}}\n"

# The base lanterns-field check: the spawnable's serialized fields must still be the base class's (the prefab is
# hand-shaped after Switchback's features prefab, which the same base serializes).
errors = []
switchback_features = open(os.path.join(lib.ROOT, "Assets/_Prefabs/Spawnables/SpawnableSwitchbackFeatures.prefab"),
                           encoding="utf-8").read()
base_fields = re.findall(r"^  (\w+):", switchback_features.split("m_EditorClassIdentifier:", 1)[1], re.M)
ours = re.findall(r"^  (\w+):", LANTERNS_PREFAB_TEXT.split("m_EditorClassIdentifier:", 1)[1], re.M)
if base_fields != ours:
    errors.append(f"SpawnableLanternRunLanterns.prefab fields {ours} no longer match the spawnable base's {base_fields}")

htr.author(dict(
    member="LanternRun", display="Lantern Run", hull="Angler", class_id=17,
    scripts={
        "LanternRunController":   f"{MODE_DIR}/LanternRunController.cs",
        "LanternRunCourse":       f"{MODE_DIR}/LanternRunCourse.cs",
        "LanternRunCourseSource": f"{MODE_DIR}/LanternRunCourseSource.cs",
        "LanternRunCourseTests":  "Assets/_Scripts/Tests/Editor/LanternRunCourseTests.cs",
        "SpawnableLanternRunLanterns": LANTERNS_SCRIPT,
    },
    doc="Assets/_Scripts/Controller/Arcade/LANTERNRUN.md",
    donor_scene=f"{lib.SCENES_DIR}/MinigameThresherTime.unity",
    donor_controller_guid=lib.guid("script/ThresherTimeController"),
    donor_rule_guid=lib.guid("asset/ThresherTimeScoringRule"),
    donor_fields=FIELDS.format(rule=lib.guid("asset/ThresherTimeScoringRule"), commit=135, lead=150, through=100,
                               speed=4600, extra=""),
    # The Angler cruises on a ~48 u circle (Thresher Time's AI numbers sit outside it). A dash carries the hull up to
    # 1.6x (0.75 x gap / 0.35 s): ~2,700 u/s on a 800 u gap. 6000 covers twice that - a ~200 u step at 60 fps.
    new_fields=FIELDS.format(rule="__RULE__", commit=135, lead=150, through=100, speed=6000,
                             extra=LANTERNS_FIELD.format()),
    max_speed=2700,
    target=16, laps=2, target_key="lanternRunGateTarget", target_after="brittleStarTimeGateTarget",
    # A quarter-of-race deficit (4 gates) x 0.45 = 1.8 element levels. The Angler's Time doubles its tamed fauna's
    # speed (x1 -> x2), so a throw - and the hull that dashes after it - goes faster: the buff lands on the race's axis.
    comeback=0.45,
    description="""Anglers only, and you travel by your school. Hold RT to paint the
    lantern past the next ring and let go to throw your fish at it, then dash after
    them straight through the ring. Throw the other half, dash again. Omni crystals
    grow the school; LT sends one to bite a rival. Two laps. Fastest domain home wins.""",
    preview_objective="Paint the lantern, throw the school, dash through the ring",
    preview_notes="OPEN-ENDED, shell-only: Headlong''s solver cut for the Angler, laid as rings and lanterns at match start, so a preview has no gates. It teaches the leapfrog: paint, throw, dash.",
    build_after="MinigameBrittleStarTime",
    extra_files={LANTERNS_PREFAB: LANTERNS_PREFAB_TEXT, LANTERNS_PREFAB + ".meta": PREFAB_META},
    extra_minted=[G_LANTERNS_PREFAB],
    extra_errors=errors,
))
