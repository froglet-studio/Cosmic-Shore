#!/usr/bin/env python3
"""
What the Honeyguide's three element games (Flitstrike / Honeycomb / Woodpecker, GAME_KIT_ROSTER.md rows 18-20) share
when they are AUTHORED - so the three generators are only the numbers and the decisions that are each mode's own.

The Butterfly trio's shape (butterfly_games_common.py), reused: all three clone ONE donor scene,
MinigameUndertow.unity, because its controller block is the shape all three controllers declare (`settings` + `rule` +
`arenaCell`, in that order) and its monitor block is the plain TurnMonitor shape. Each generator swaps the identity
(controller, monitor, settings, rule), the four AI hulls (Scarab -> Honeyguide), the cells, the spawn ring and the
crystal volume. The donor is asserted, not trusted (`lib.swap_guid` / `lib.replace_block` fail loudly if Undertow's
scene moves), and once a mode's scene is committed the clone STANDS DOWN on a drifted donor
(`butterfly_games_common.clone_or_stand_down`) instead of aborting every check below it.

Not a generator itself - imported by author_flitstrike_assets.py, author_honeycomb_assets.py and
author_woodpecker_assets.py.
"""
import os

import arcade_mode_lib as lib
import butterfly_games_common as bgc

guid = lib.guid

DONOR = bgc.DONOR
DONOR_CELLS = bgc.DONOR_CELLS
cell_list = bgc.cell_list
spawn_block = bgc.spawn_block
clone_or_stand_down = bgc.clone_or_stand_down

HONEYGUIDE_ID = lib.VESSEL_CLASS_ID["Honeyguide"]
SCARAB_ID = lib.VESSEL_CLASS_ID["Scarab"]
# SO_Class_Honeyguide - what Slip Line's card lists; read off the asset so a regenerated class cannot drift.
VESSEL_HONEYGUIDE = lib.existing_guid("Assets/_SO_Assets/Classes/SO_Class_Honeyguide.asset")

# The Honeyguide's kit, read (never edited) by every generator: the mode rests on these, so each one asserts what
# its game needs from them (the LOCKED vessel rule - a mode reads the kit, it does not change it).
HONEYGUIDE_PREFAB = "Assets/_Prefabs/Spacevessels/Honeyguide.prefab"
CREW_TRIGGERS = "Assets/_Scripts/Controller/Vessel/Crew/HoneyguideCrew.Triggers.cs"
CREW_HUNT = "Assets/_Scripts/Controller/Vessel/Crew/HoneyguideCrew.Hunt.cs"


def clone_scene(controller_guid, monitor_guid, settings_guid, rule_guid,
                cells, cell_choice, spawn, no_nucleus_radius, monitor_extra="") -> str:
    """The donor with the identity, cells, AI hulls, spawn ring and crystal volume swapped (butterfly_games_common's
    clone, with the Honeyguide in the four AI seats)."""
    with open(os.path.join(lib.ROOT, bgc.DONOR_SCENE), encoding="utf-8") as fh:
        scene = fh.read()
    scene = lib.swap_guid(scene, DONOR["UndertowPointTurnMonitor"], monitor_guid, "turn monitor")
    scene = lib.swap_guid(scene, DONOR["UndertowController"], controller_guid, "controller")
    scene = lib.swap_guid(scene, DONOR["UndertowSettings"], settings_guid, "settings")
    scene = lib.swap_guid(scene, DONOR["UndertowScoringRule"], rule_guid, "scoring rule")
    scene = lib.replace_block(scene,
                              "  CellConfigs:\n" + cell_list(DONOR_CELLS) + "  cellTypeChoiceOptions: 1\n",
                              "  CellConfigs:\n" + cell_list(cells) + f"  cellTypeChoiceOptions: {cell_choice}\n",
                              "cell config list")
    scene = lib.swap_guid(scene, f"  - vesselClass: {SCARAB_ID}\n    PlayerName: AI",
                          f"  - vesselClass: {HONEYGUIDE_ID}\n    PlayerName: AI", "AI templates", expected=4)
    scene = lib.replace_block(scene, bgc.DONOR_SPAWN, spawn, "spawn ring")
    scene = lib.replace_block(scene, bgc.DONOR_NO_NUCLEUS, f"  noNucleusSpawnRadius: {no_nucleus_radius}\n",
                              "crystal volume")
    if monitor_extra:
        scene = lib.replace_block(scene, bgc.DONOR_MONITOR_TAIL,
                                  "  _updateInterval: 1\n" + monitor_extra +
                                  "  gameData: {fileID: 11400000, guid: b35f33752bb10a44cb5033b5670f50aa, type: 2}\n",
                                  "monitor fields")
    return scene


def assert_scene(errors, scene, must_have: dict, must_not_have: dict):
    if scene is None:
        return
    for name, gd in must_have.items():
        if gd not in scene:
            errors.append(f"cloned scene missing {name}")
    for name, gd in must_not_have.items():
        if gd in scene:
            errors.append(f"cloned scene still references {name}")
    if scene.count(f"  - vesselClass: {HONEYGUIDE_ID}\n") != 4 or f"  - vesselClass: {SCARAB_ID}\n" in scene:
        errors.append("cloned scene does not carry exactly 4 Honeyguide AI templates")


def kit_errors(g, needs) -> "list[str]":
    """The Honeyguide kit facts a game rests on. `needs` names which: 'strike' (a Flit raises a Strike-class combat
    hit), 'peck' (the AI pecks a hostile prism ahead), 'yield' (the crew's freestyle hunt yields to a match's steering
    hook), 'hive' (an omni crystal opens hive mode)."""
    errs = []
    triggers = g.read(CREW_TRIGGERS)
    hunt = g.read(CREW_HUNT)
    if "strike" in needs and ("const CombatHitClass hitClass = CombatHitClass.Strike;" not in triggers
                              or "OnCombatHitLanded" not in triggers):
        errs.append("HoneyguideCrew.StrikeVessel no longer raises a Strike-class combat hit - Flitstrike would score nothing")
    if "peck" in needs and "PeckTargetAhead(pos, fwd)" not in triggers:
        errs.append("the Honeyguide's AI no longer pecks a hostile prism ahead (PeckTargetAhead) - its AI would not score")
    if "yield" in needs and "return hook == null || ReferenceEquals(hook, _huntSteer);" not in hunt:
        errs.append("the Honeyguide's freestyle hunt no longer yields to a match's steering hook - the mode AI would fight it")
    if "hive" in needs:
        hive = g.read("Assets/_Scripts/Controller/Vessel/Crew/HoneyguideCrew.Hive.cs")
        if "prism.ActivateSuperShield();" not in hive or "string owner = x.Dom == Own ? _status.PlayerName" not in hive:
            errs.append("the hive comb no longer lays the pilot's own cells as owned, super-shielded prisms - "
                        "Honeycomb's premise moved")
    return errs
