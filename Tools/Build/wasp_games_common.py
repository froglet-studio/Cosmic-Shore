#!/usr/bin/env python3
"""
What the Jewel Wasp's arcade games (Marionette - Charge, Gorge - Space; GameModes 126-127, reserved by
Docs/ElementalLoop/GAME_KIT_ROSTER.md rows 36-37) share: the wasp's identity, the one shared AI helper (WaspSeats.cs,
whose .meta every one of the generators authors identically), and the load-bearing reads of the wasp's own executor and
config that every mode AI leans on.

NOTHING HERE EDITS THE WASP (the LOCKED vessel rule, Shombith 2026-10-31). The checks below fail a generator when the
vessel stops doing what a mode was built on, so the drift is a named failure rather than a silently dead game.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402
from shrike_games_common import cell_list, ladder_errors, ladder_yaml  # noqa: E402,F401  (shared helpers, read-only)

AR = "Assets/_Scripts/Controller/Arcade"
SHARED_DIR = f"{AR}/WaspGames"
SHARED_SCRIPTS = {"WaspSeats": f"{SHARED_DIR}/WaspSeats.cs"}

WASP_VESSEL = "f4dcdd79b01f4c8d8402b5f47983d529"            # SO_Class_JewelWasp.asset
WASP_ID = 31                                                 # VesselClassType.JewelWasp
WASP_EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/JewelWaspExecutor.cs"
WASP_CONFIG = "Assets/_SO_Assets/VesselActions/JewelWasp/JewelWaspConfig.asset"
VESSEL_CLASS_CS = "Assets/_Scripts/Data/Enums/VesselClassType.cs"
COMBAT_CHANNEL = "45fb43a0cddb5cc3cc4dc448df994152"          # the game's combat-hit channel

# The wasp's own AI, which every mode AI relies on: steering-only, because the hull stings, rides and presses itself.
AUTOPILOT_NEEDLES = (
    "if (IsAI) StepAIFree();",
    "var f = FindFaunaAhead(hull.position, hull.forward, 35f, out _) ?? FindFaunaAhead(hull.position, hull.forward, 180f, out _);",
    "if (IsAI) StepAIHost(dt);",
    "var rival = NearestRival(host.PossessionNestPoint, _frame * Vector3.forward, 400f, 180f);",
    "if (Time.time >= _suctionAt) HostRight();",
    "public WaspMode Mode => _mode;",
)


def emit_shared(g):
    """The shared AI helper's .meta and folder - identical from every wasp game generator."""
    for k, p in SHARED_SCRIPTS.items():
        g.script_meta(p, lib.guid(f"script/{k}"))
    g.folder_meta(SHARED_DIR)
    return [lib.guid(f"script/{k}") for k in SHARED_SCRIPTS] + [lib.guid(f"folder/{SHARED_DIR}")]


def common_errors(g, ai_rel, mode_name):
    """The checks every wasp game makes: the hull still flies itself the way the mode AI assumes, and the mode AI is
    steering-only and keyed on the seat's difficulty."""
    errors = []
    executor = g.read(WASP_EXECUTOR)
    for needle in AUTOPILOT_NEEDLES:
        if needle not in executor:
            errors.append(f"JewelWaspExecutor no longer has '{needle}' - {mode_name}'s AI only steers the free hull and "
                          f"leans on the wasp's own AI to sting, ride and press; re-check how the wasp flies itself")
    if f"JewelWasp = {WASP_ID}," not in g.read(VESSEL_CLASS_CS):
        errors.append(f"VesselClassType.JewelWasp is no longer {WASP_ID}")
    seats = g.read(SHARED_SCRIPTS["WaspSeats"])
    for src, name in ((g.read(ai_rel), ai_rel), (seats, SHARED_SCRIPTS["WaspSeats"])):
        if "PerformShipControllerActions" in src or "InputStatus" in src or "StopShipControllerActions" in src \
                or ".Press(" in src:
            errors.append(f"{name} presses something - the wasp's own AI owns every press")
        if "SelectedIntensity" in src:
            errors.append(f"{name} reads the intensity - an AI's skill is the seat's AI difficulty")
    if "AIDifficultyFor(" not in seats:
        errors.append("WaspSeats no longer keys the seat's level on gameData.AIDifficultyFor")
    if "SetExternalTargetProvider" not in seats or "ClearExternalTargetProvider" not in seats:
        errors.append("WaspSeats no longer steers through (and hands back) the external target provider")
    if "s.Free" not in g.read(ai_rel):
        errors.append(f"{ai_rel} steers a wasp that is not free - inside a host the wasp's own AI flies the creature")
    return errors
