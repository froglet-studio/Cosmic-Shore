#!/usr/bin/env python3
"""
What the Shrike's three arcade games (Larder - Charge, Thornbush - Mass, Lancefall - Space; GameModes 102-104, reserved
by Docs/ElementalLoop/GAME_KIT_ROSTER.md rows 12-14) share: the Shrike's identity, the one shared AI helper
(ShrikeSeats.cs, which every one of the three generators authors the .meta of, identically), and the load-bearing reads
of the Shrike's own executor and config that every mode AI leans on.

NOTHING HERE EDITS THE SHRIKE (the LOCKED vessel rule, Shombith 2026-10-31). The checks below fail a generator when the
vessel stops doing what a mode was built on, so the drift is a named failure rather than a silently dead game.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

AR = "Assets/_Scripts/Controller/Arcade"
SHARED_DIR = f"{AR}/ShrikeGames"
SHARED_SCRIPTS = {"ShrikeSeats": f"{SHARED_DIR}/ShrikeSeats.cs"}

SHRIKE_VESSEL = "bbb0b0e8c8f85ba69d31e63fe9e05d77"          # SO_Class_Shrike.asset
SHRIKE_ID = lib.VESSEL_CLASS_ID["Shrike"]
SHRIKE_EXECUTOR = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShrikeExecutor.cs"
SHRIKE_CONFIG = "Assets/_SO_Assets/VesselActions/Shrike/ShrikeConfig.asset"
CRUISE_SPEED = 18.0   # u/s, SHRIKE.md §3.6 ("Cruise is the design ship's 18 u/s")


def emit_shared(g):
    """The shared AI helper's .meta and folder - identical from every Shrike game generator."""
    for k, p in SHARED_SCRIPTS.items():
        g.script_meta(p, lib.guid(f"script/{k}"))
    g.folder_meta(SHARED_DIR)
    return [lib.guid(f"script/{k}") for k in SHARED_SCRIPTS] + [lib.guid(f"folder/{SHARED_DIR}")]


def config_value(g, key):
    """A scalar off ShrikeConfig.asset (`  key: value`)."""
    m = re.search(rf"^  {key}: ([-0-9.e]+)$", g.read(SHRIKE_CONFIG), re.M)
    assert m, f"{key} not found in {SHRIKE_CONFIG}"
    return float(m.group(1))


def config_range(g, key):
    """(Min, Max) of an ElementalFloat on ShrikeConfig.asset."""
    m = re.search(rf"^  {key}:\n(?:    .*\n)*?    Min: ([-0-9.e]+)\n    Max: ([-0-9.e]+)\n", g.read(SHRIKE_CONFIG), re.M)
    assert m, f"{key} (ElementalFloat) not found in {SHRIKE_CONFIG}"
    return float(m.group(1)), float(m.group(2))


# The autopilot behaviours every mode AI relies on: it is steering-only because the hull presses for itself.
AUTOPILOT_NEEDLES = (
    "if (_status.AutoPilotEnabled && IsAuthority()) Autopilot();",
    "Vector3 toTarget = ai.TargetPosition - pos;",
    "Mode want = _aiEnemyAhead && MissilesReady > 0 ? Mode.Charge",
    ": _aiTargetsAhead >= config.AiLanceMinTargets ? Mode.Space",
    "if (Docked && _dockedFor >= config.AiDockSeconds && _slingCd <= 0f",
    "public int MissilesReady",
)


def common_errors(g, ai_rel, mode_name):
    """The checks every Shrike game makes: the hull still flies itself the way the mode AI assumes, and the mode AI is
    steering-only and keyed on the seat's difficulty."""
    errors = []
    executor = g.read(SHRIKE_EXECUTOR)
    for needle in AUTOPILOT_NEEDLES:
        if needle not in executor:
            errors.append(f"ShrikeExecutor no longer has '{needle}' - {mode_name}'s AI only steers and leans on the "
                          f"hull's own autopilot to press; re-check how the Shrike flies itself")
    seats = g.read(SHARED_SCRIPTS["ShrikeSeats"])
    for src, name in ((g.read(ai_rel), ai_rel), (seats, SHARED_SCRIPTS["ShrikeSeats"])):
        if "PerformShipControllerActions" in src or "InputStatus" in src or "StopShipControllerActions" in src:
            errors.append(f"{name} presses something - the Shrike's own autopilot owns every press")
        if "SelectedIntensity" in src:
            errors.append(f"{name} reads the intensity - an AI's skill is the seat's AI difficulty")
    if "AIDifficultyFor(" not in seats:
        errors.append("ShrikeSeats no longer keys the seat's level on gameData.AIDifficultyFor")
    if "SetExternalTargetProvider" not in seats or "ClearExternalTargetProvider" not in seats:
        errors.append("ShrikeSeats no longer steers through (and hands back) the external target provider")
    return errors


def ladder_errors(settings_cs, ladders, shared, cls):
    errors = []
    for name, (values, harder) in ladders.items():
        if len(values) != 4:
            errors.append(f"{name}: needs four rungs")
        pairs = list(zip(values, values[1:]))
        if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
            errors.append(f"{name}: not monotone toward harder ({values})")
        init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
        if f"{name} = {init};" not in settings_cs:
            errors.append(f"{cls}.{name}: the C# initializer does not match {init}")
    for field, value in shared.items():
        if f"{field} = {lib.num(float(value))}f;" not in settings_cs:
            errors.append(f"{cls}.{field} initializer differs from the authored {value}")
    return errors


def ladder_yaml(ladders, name):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in ladders[name][0])


def cell_list(guids):
    return "".join(f"  - {{fileID: 11400000, guid: {gd}, type: 2}}\n" for gd in guids)


def spawn_block(rings):
    return ("  arrangeSpawnPointsAroundCell: 1\n  spawnDistanceOutsideNucleus: 40\n"
            f"  spawnRingRadiusFloor: {rings[0]}\n  spawnRingRadiusFloorByIntensity:\n"
            + "".join(f"  - {v}\n" for v in rings) + "  spawnFormation: 0\n")


def omni_block(ball, extras, per_player=None):
    per_player = per_player or [0, 0, 0, 0]
    return (f"  noNucleusSpawnRadius: {ball}\n  crystalCountMode: 2\n  fixedCrystalCount: 1\n"
            "  extraCrystalsToSpawnBeyondPlayerCount: 0\n  crystalCountByIntensity:\n"
            + "".join(f"  - CrystalsPerPlayer: {p}\n    ExtraCrystals: {e}\n" for p, e in zip(per_player, extras)))

SHRIKE_PREFAB = "Assets/_Prefabs/Spacevessels/Shrike.prefab"


def cruise_trail_block(g):
    """One cruising trail block's volume, u^3: the Shrike prefab's prism BaseScale times its trailVolume (the prism
    controller's ElementalFloat, Value while it is off), the block the game actually lays."""
    text = g.read(SHRIKE_PREFAB)
    m = re.search(r"BaseScale: \{x: ([-0-9.e]+), y: ([-0-9.e]+), z: ([-0-9.e]+)\}", text)
    assert m, f"no prism BaseScale in {SHRIKE_PREFAB}"
    tv = re.search(r"\n  trailVolume:\n    Enabled: 0\n    Value: ([-0-9.e]+)\n", text)
    return float(m.group(1)) * float(m.group(2)) * float(m.group(3)) * (float(tv.group(1)) if tv else 1.0)


def nest_volume(g, count):
    """One nest's raw box volume, u^3, at `count` shell plates: a port of ShrikeRules.NestPieces (shell plates, the
    weave of twigs, the lining), read off ShrikeConfig.asset. Box volume, no shield extra (what Thornbush scores)."""
    import math
    side, t, cl = config_value(g, "nestBlockSize"), config_value(g, "nestShellThickness"), config_value(g, "nestClearance")
    hull_r, hoops = config_value(g, "hullRadius"), int(config_value(g, "nestHoops"))
    w, L, lining, ls = (config_value(g, k) for k in ("nestTwigWidth", "nestTwigLength", "nestLining", "nestLiningSize"))

    def local(i, n, r, zmin=-0.25):
        z = 1 - (i + .5) / n * (1 - zmin)
        rr = math.sqrt(max(0.0, 1 - z * z))
        return (rr * math.cos(i * 2.39996) * r, rr * math.sin(i * 2.39996) * r, -z * r)

    pts = [local(i, count, 1.0) for i in range(count)]
    sp = min((math.dist(a, b) for i, a in enumerate(pts) for b in pts[i + 1:]), default=2.0)
    radius = max(9.0, side * math.sqrt(3) * cl / sp)
    plate = max(side, radius * sp / (math.sqrt(2) * cl))       # ShrikeRules.NestPlateSide
    vol = count * plate * plate * t
    inner, hull = radius - t * .5 * cl - .5, hull_r + .5
    if hoops > 0 and inner - w > hull + w:
        ri = .5 * (hull + inner) + .5
        max_half = math.sqrt(max(0.0, (inner - w) ** 2 - ri * ri))
        for h in range(hoops):
            th = math.radians(65 if hoops == 1 else 35 + 60 * h / (hoops - 1))
            rho = ri * math.sin(th)
            m = max(5, int(math.floor(2 * math.pi * rho / max(.5, L * 1.15) + .5)))
            vol += m * w * w * min(2 * rho * math.sin(math.pi / m) * .85, 2 * max_half)
    return vol + int(lining) * ls * ls * 0.4
