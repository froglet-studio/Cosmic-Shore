#!/usr/bin/env python3
"""
What the three Butterfly element games (Dustup / Tapestry / Sirocco) share when they are AUTHORED
- so the three generators are only the numbers and the decisions that are each mode's own.

All three clone ONE donor scene, MinigameUndertow.unity, because its controller block is the
shape all three controllers declare (`settings` + `rule` + `arenaCell`, in that order) and its
monitor block is the plain TurnMonitor shape. Each generator then swaps the identity (controller,
monitor, settings, rule), the four AI hulls (Scarab -> Butterfly) and whatever its own arena
needs. The donor is asserted, not trusted: `swap_guid` / `replace_block` fail LOUDLY if Undertow's
scene moves, rather than producing a half-wired clone - and once a mode's scene is committed, the
generator STANDS DOWN on a donor that has drifted (`clone_or_stand_down`) instead of aborting every
check below it (the author_dogfight_assets.py trap).

Not a generator itself - imported by author_dustup_assets.py, author_tapestry_assets.py and
author_sirocco_assets.py.
"""
import os
import re

import arcade_mode_lib as lib

guid = lib.guid

DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameUndertow.unity"

# The donor's mode-specific wiring - minted by author_undertow_assets.py, so the same guid
# function reproduces it; every generator asserts these resolve like any other reference.
DONOR = {
    "UndertowController":       guid("script/UndertowController"),
    "UndertowPointTurnMonitor": guid("script/UndertowPointTurnMonitor"),
    "UndertowSettings":         guid("asset/UndertowSettings"),
    "UndertowScoringRule":      guid("asset/UndertowScoringRule"),
}

WL_DIR = "Assets/_SO_Assets/Cell Configs/Wildlife Liberation Cell"
DONOR_CELLS = [lib.existing_guid(f"{WL_DIR}/Wildlife Liberation Cell Config {i}.asset") for i in range(1, 5)]
DONOR_SPAWN = "  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n  spawnRingRadiusFloor: 1150\n  spawnFormation: 1\n"
DONOR_NO_NUCLEUS = "  noNucleusSpawnRadius: 480\n"
DONOR_MONITOR_TAIL = "  _updateInterval: 1\n  gameData: {fileID: 11400000, guid: b35f33752bb10a44cb5033b5670f50aa, type: 2}\n"

BUTTERFLY_ID = lib.VESSEL_CLASS_ID["Butterfly"]


def cell_list(guids) -> str:
    return "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in guids)


def spawn_block(distance, floor, formation) -> str:
    return (f"  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: {distance}\n"
            f"  spawnRingRadiusFloor: {floor}\n  spawnFormation: {formation}\n")


def clone_scene(controller_guid, monitor_guid, settings_guid, rule_guid,
                cells, cell_choice, spawn, no_nucleus_radius, monitor_extra="") -> str:
    """The donor with the identity, cells, AI hulls, spawn ring and crystal volume swapped.
    `cells` is the new CellConfigs list; `cell_choice` its CellTypeChoiceOptions (1 =
    IntensityWise over four configs, 0 = a single config). `monitor_extra` is inserted after the
    monitor's `_updateInterval` (a timed monitor's `duration`)."""
    with open(os.path.join(lib.ROOT, DONOR_SCENE), encoding="utf-8") as fh:
        scene = fh.read()
    scene = lib.swap_guid(scene, DONOR["UndertowPointTurnMonitor"], monitor_guid, "turn monitor")
    scene = lib.swap_guid(scene, DONOR["UndertowController"], controller_guid, "controller")
    scene = lib.swap_guid(scene, DONOR["UndertowSettings"], settings_guid, "settings")
    scene = lib.swap_guid(scene, DONOR["UndertowScoringRule"], rule_guid, "scoring rule")
    scene = lib.replace_block(scene,
                              "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(cells) + f"  cellTypeChoiceOptions: {cell_choice}\n",
                              "cell config list")
    scene = lib.swap_guid(scene, "  - vesselClass: 12\n    PlayerName: AI",
                          f"  - vesselClass: {BUTTERFLY_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, DONOR_SPAWN, spawn, "spawn ring")
    scene = lib.replace_block(scene, DONOR_NO_NUCLEUS, f"  noNucleusSpawnRadius: {no_nucleus_radius}\n",
                              "crystal volume")
    if monitor_extra:
        scene = lib.replace_block(scene, DONOR_MONITOR_TAIL,
                                  "  _updateInterval: 1\n" + monitor_extra +
                                  "  gameData: {fileID: 11400000, guid: b35f33752bb10a44cb5033b5670f50aa, type: 2}\n",
                                  "monitor fields")
    return scene


def clone_or_stand_down(g, scene_name, scene_guid, errors, build):
    """Build the clone; if the DONOR has drifted since this mode's scene was committed, keep the
    committed scene (registered as this run's output so --check describes it) and say so, rather
    than aborting every check below the clone."""
    rel = f"{lib.SCENES_DIR}/{scene_name}.unity"
    try:
        scene = build()
    except AssertionError as e:
        if os.path.exists(os.path.join(lib.ROOT, rel)):
            print(f"note: scene clone stood down - the Undertow donor has moved on ({e}) and "
                  f"{scene_name}.unity is already committed. Every other section still runs.")
            scene = g.read(rel)
        else:
            errors.append(f"scene clone failed and no committed {scene_name}.unity exists: {e}")
            return None
    g.emit_scene(scene_name, scene_guid, scene)
    return scene


def card_background(card_rel: str) -> str:
    """The card's CardBackground guid: whatever the /cardart pipeline already wrote onto the card,
    else the shared placeholder. A generator that re-emitted the placeholder would silently undo a
    rendered background on every run - the drift author_undertow_assets.py --check now reports."""
    full = os.path.join(lib.ROOT, card_rel)
    if os.path.exists(full):
        m = re.search(r"^  CardBackground: \{fileID: 21300000, guid: ([0-9a-f]{32}), type: 3\}",
                      open(full, encoding="utf-8").read(), re.M)
        if m:
            return m.group(1)
    return lib.CARD_ART["CardBackground"]


def assert_scene(errors, scene, must_have: dict, must_not_have: dict):
    if scene is None:
        return
    for name, gd in must_have.items():
        if gd not in scene:
            errors.append(f"cloned scene missing {name}")
    for name, gd in must_not_have.items():
        if gd in scene:
            errors.append(f"cloned scene still references {name}")
    if scene.count(f"  - vesselClass: {BUTTERFLY_ID}\n") != 4 or "  - vesselClass: 12\n" in scene:
        errors.append("cloned scene does not carry exactly 4 Butterfly AI templates")
