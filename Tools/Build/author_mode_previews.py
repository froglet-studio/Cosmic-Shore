#!/usr/bin/env python3
"""
Author the ModePreview_<Mode>.asset definitions for the arcade cards that shipped WITHOUT one.

Eight playable cards (each with a real scene on disk and an entry on the Arcade grid) had no
`ModePreviewDefinitionSO`, so their cards fell back to the static background sprite and offered no
Test Flight: Salvo(44), Switchback(45), Hijack(46), Headlong(49), Breakwater(50), Skein(51),
Bloomrush(52) and Redline(53). Every one of their generators pre-dates `arcade_mode_lib`'s
`register_preview` step, which is how a registration that every LATER mode makes was missed by
the eight that came before it.

The rule the definitions follow is the platform's: point at the MODE'S OWN cell configs, read off
the mode's own SCENE (the scene is the authority - a second list is a second answer), and let
`author_preview_spawns.py` own the spawn block. Nothing here forks a cell or invents a structure:
a mode whose structure is CONTROLLER-built (the gate races' rings, Breakwater's stations) previews
its cell only, exactly as Tollway, Regatta and Astro League already do, and the Notes say so.

Usage:
    python3 Tools/Build/author_mode_previews.py            # write
    python3 Tools/Build/author_mode_previews.py --check    # verify only (exit 1 on drift)
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

ROOT = lib.ROOT
PREVIEW_DIR = "Assets/_SO_Assets/Mode Previews"
SCENES = "Assets/_Scenes/Multiplayer Scenes"

# ScoringMetric members (Assets/_Scripts/Data/Enums/ScoringMetric.cs) - read, never guessed.
def metric(name: str) -> int:
    src = open(os.path.join(ROOT, "Assets/_Scripts/Data/Enums/ScoringMetric.cs"), encoding="utf-8").read()
    m = re.search(rf"^\s*{name}\s*=\s*(\d+)", src, re.M)
    assert m, f"ScoringMetric.{name} not found"
    return int(m.group(1))


def vessel(name: str) -> int:
    src = open(os.path.join(ROOT, "Assets/_Scripts/Data/Enums/VesselClassType.cs"), encoding="utf-8").read()
    m = re.search(rf"^\s*{name}\s*=\s*(-?\d+)", src, re.M)
    assert m, f"VesselClassType.{name} not found"
    return int(m.group(1))


def game_mode(name: str) -> int:
    src = open(os.path.join(ROOT, "Assets/_Scripts/Data/Enums/GameModes.cs"), encoding="utf-8").read()
    m = re.search(rf"^\s*{name}\s*=\s*(\d+)", src, re.M)
    assert m, f"GameModes.{name} not found"
    return int(m.group(1))


CELL_BLOCK = re.compile(
    r"^  CellConfigs:\n((?:  - \{fileID: \d+, guid: [0-9a-f]{32}, type: \d+\}\n)+)"
    r"  cellTypeChoiceOptions: (\d+)", re.M)


def scene_cells(scene_name: str):
    """(configs, intensity_wise) off the scene's Cell - the same read author_preview_intensities
    makes, so the two can never disagree about which arena a card shows."""
    text = open(os.path.join(ROOT, SCENES, scene_name + ".unity"), encoding="utf-8", errors="replace").read()
    blocks = CELL_BLOCK.findall(text)
    assert len(blocks) == 1, f"{scene_name}: expected exactly one Cell config block, found {len(blocks)}"
    block, choice = blocks[0]
    refs = [line.strip()[2:] for line in block.strip().split("\n")]
    return refs, choice == "1"


# ── The eight cards ──────────────────────────────────────────────────────────
# name, mode, scene, vessel, objective text, metric, notes
MODES = [
    ("Salvo", "Salvo", "MinigameSalvo", "Sparrow",
     "Tear the wreckage apart",
     "PrismsDestroyed",
     "Dog Fight's Boneyard referenced outright - the cell is per-arena, not per-mode, so the "
     "four intensities are Dog Fight's four. Prisms destroyed fires solo, so the objective "
     "COUNTS: the guns and the skyburst both pay here, and every hostile prism refills the "
     "missile bay. No nucleus, so SpawnRingRadiusFloor carries the whole standoff."),
    ("Switchback", "Switchback", "MinigameSwitchback", "Dolphin",
     "Thread the rings in order",
     "SwitchesThreaded",
     "The LOOKING phase is the Barren cell; the FLIGHT phase stands the mode's own rings "
     "(ModePreviewGateCourse, off the same RaceCourseSource the controller builds from, on the "
     "card-art seed) and starts the pilot behind gate 1. Crossings count locally into the "
     "objective box; the race loops. Intensity is the COURSE, not the arena, so one cell serves "
     "every intensity and an intensity nudge re-stands only the rings."),
    ("Hijack", "Hijack", "MinigameHijack", "Urchin",
     "Grind a rail and steal it",
     "PrismsStolen",
     "The Switchyard is an authored EnvironmentPrefab, one per intensity (burr mass scales, the "
     "rails never move), so the scale model shows the three rings and the burrs and the flight "
     "preview lets the Urchin grind a rail and launch into a burr. Prisms stolen fires solo, so "
     "the objective COUNTS."),
    ("Headlong", "Headlong", "MinigameHeadlong", "Rhino",
     "Lap the circuit, thread every gate",
     "SwitchesThreaded",
     "The FLIGHT phase stands the circuit the controller would solve from the Rhino's own turn "
     "curve (same RaceCourseSource, card-art seed), laps it as the match does, and starts the "
     "pilot behind gate 1. One cell serves every intensity (intensity is the corner mix)."),
    ("Breakwater", "Breakwater", "MinigameBreakwater", "Sparrow",
     "Fire, saw or thread a station",
     "SwitchesThreaded",
     "The LOOKING phase shows the cell's own four configs - no EnvironmentPrefab, no nucleus. The "
     "FLIGHT phase stands the start gate and the circuit (same RaceCourseSource, card-art seed) "
     "AND the stations: CourseStructurePrefab is the scene controller's own SpawnableBreakwater, "
     "posed by the same source call, so the dishes and danger weave are the match's. The pilot "
     "starts behind the start gate."),
    ("Skein", "Skein", "MinigameSkein", "Urchin",
     "Ride the cable, change strands at the breaks",
     "SwitchesThreaded",
     "The knot is an authored EnvironmentPrefab, one per intensity, so the scale model shows "
     "the (2,3) torus knot cable and the flight preview lets the Urchin ride a strand and take "
     "an aimed launch across a break. The FLIGHT phase also stands the rings on the cable (same "
     "RaceCourseSource) and starts the pilot on the match's own start line, behind the collar."),
    ("Bloomrush", "Bloomrush", "MinigameBloomrush", "Manta",
     "Skim the reef, tag wildlife, cash out on a crystal",
     "VolumeDestroyed",
     "Rampage's cactus forest referenced outright - the mode itself references the same four "
     "configs (the Bends precedent), and the forest is identical at every intensity (only the "
     "crystal ladder and the fuse vary, neither of which a satellite can draw), so the "
     "intensity list is authored but visually static. Volume destroyed fires solo, so the "
     "objective COUNTS once a bloom lands; the satellite's own life spawner is suppressed, so "
     "there is nothing to tag - the reef and the crystals are the taste."),
    ("Redline", "Redline", "MinigameRedline", "Manta",
     "Lap the circuit flat out",
     "SwitchesThreaded",
     "Headlong's solver cut to the Manta's full-boost circle: the FLIGHT phase stands that "
     "circuit (same RaceCourseSource, card-art seed), laps it, and starts the pilot behind gate "
     "1. It teaches the Soar/Yastri trigger trade. One cell serves every intensity."),
]

def course_structure(scene_name: str) -> str:
    """The structure the scene's race controller hangs on its course (Breakwater's
    `arenaPrefab`), read off the scene - the scene is the authority. `{fileID: 0}` for the rest."""
    text = open(os.path.join(ROOT, SCENES, scene_name + ".unity"), encoding="utf-8").read()
    m = re.search(r"^  arenaPrefab: (\{fileID: -?\d+, guid: [0-9a-f]{32}, type: \d+\})$", text, re.M)
    return m.group(1) if m else "{fileID: 0}"


def _wrap(text: str, width: int = 88):
    words, lines, cur = text.split(), [], ""
    for w in words:
        if cur and len(cur) + 1 + len(w) > width:
            lines.append(cur)
            cur = w
        else:
            cur = f"{cur} {w}" if cur else w
    if cur:
        lines.append(cur)
    return lines


def _spawn_block(scene: str) -> str:
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import author_preview_spawns as sp
    cell_guids = {sp.script_guid("Assets/_Scripts/Controller/Environment/Cell.cs")}
    data = sp.read_scene_spawn(os.path.join(ROOT, SCENES, scene + ".unity"), cell_guids)
    assert data is not None, f"{scene}: no spawn initializer"
    return sp.spawn_block(data)


g = lib.Generator(0, "ModePreviews")
errors, referenced, minted = [], dict(lib.SCRIPT_GUIDS), []

# The preview library must already carry every OTHER shipped definition; this generator only
# appends, so a definition it does not author is left exactly where it is.
library_before = g.read(lib.PREVIEW_LIBRARY)

for name, mode_name, scene, hull, objective, metric_name, notes in MODES:
    mode_id = game_mode(mode_name)
    card = f"Assets/_SO_Assets/Games/ArcadeGame{name}.asset"
    card_text = g.read(card)
    assert re.search(rf"^  Mode: {mode_id}$", card_text, re.M), f"{card} is not mode {mode_id}"
    assert re.search(rf"^  SceneName: {scene}$", card_text, re.M), f"{card} does not launch {scene}"

    # The card's vessel lock must agree with the hull the preview pins - a preview flying a hull
    # the mode refuses would be teaching the wrong ship.
    hull_guid = lib.VESSELS[hull]
    assert hull_guid in card_text, f"{card} does not list {hull}"
    vessel_id = vessel(hull)

    configs, intensity_wise = scene_cells(scene)
    for ref in configs:
        gm = re.search(r"guid: ([0-9a-f]{32})", ref)
        referenced[f"{name} cell {gm.group(1)[:8]}"] = gm.group(1)
    if intensity_wise:
        assert len(configs) == 4, f"{scene}: IntensityWise cell with {len(configs)} configs"
        by_intensity = "  PreviewCellsByIntensity:\n" + "".join(f"  - {r}\n" for r in configs)
    else:
        assert len(configs) == 1, f"{scene}: non-IntensityWise cell with {len(configs)} configs"
        by_intensity = "  PreviewCellsByIntensity: []\n"

    asset_name = f"ModePreview_{name}"
    asset_guid = lib.guid(f"asset/{asset_name}")
    minted.append(asset_guid)

    wrapped = "\n".join("    " + line for line in _wrap(notes.replace("'", "''")))
    body = (lib.header_for(lib.SCRIPT_GUIDS["ModePreviewDefinitionSO"], asset_name) +
            f"  Mode: {mode_id}\n"
            f"  Notes: '{wrapped.strip()}'\n"
            f"  PreviewCell: {configs[0]}\n"
            f"{by_intensity}"
            f"  StructurePrefab: {{fileID: 0}}\n"
            f"  TrackSpawnablesByIntensity: []\n"
            f"  CourseStructurePrefab: {course_structure(scene)}\n"
            f"  PreviewFauna: {{fileID: 0}}\n"
            f"  PreviewFaunaCount: 4\n"
            f"  Vessel: {vessel_id}\n"
            f"  ObjectiveText: {objective}\n"
            f"  ObjectiveMetric: {metric(metric_name)}\n"
            f"  ObjectiveTarget: 0\n"
            f"  DurationSeconds: 60\n")
    # Spawn block: author_preview_spawns.py owns it and reads the scene. Emit the same block it
    # would, so this generator's --check and that one's agree byte for byte.
    body += _spawn_block(scene)
    g.emit_asset(f"{PREVIEW_DIR}/{asset_name}.asset", asset_guid, body)
    g.register_preview(asset_guid)

# Every definition that was in the library before this run must still be there.
library_after = g.read_current(lib.PREVIEW_LIBRARY)
for line in re.findall(r"^  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}$", library_before, re.M):
    if line not in library_after:
        errors.append(f"preview library lost an existing definition: {line.strip()}")

g.finish(errors, referenced, minted)
