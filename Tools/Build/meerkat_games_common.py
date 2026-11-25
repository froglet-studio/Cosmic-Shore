#!/usr/bin/env python3
"""
What the Meerkat's three arcade games (Sentry 105, Warren 106, Cave-in 107; Docs/ElementalLoop/GAME_KIT_ROSTER.md rows
15-17) share in their generators: the shared AI hook's metas, the Meerkat kit files every game's assumptions are read
from, and the ladder / steering / enum checks each one runs. Imported by author_sentry_assets.py,
author_warren_assets.py and author_cavein_assets.py; not a generator itself.

Run it to self-test the checks against a negative control:   python3 Tools/Build/meerkat_games_common.py
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib  # noqa: E402

guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
SHARED_DIR = f"{AR}/MeerkatGames"
STEER = f"{SHARED_DIR}/MeerkatArcadeSteer.cs"
SHARED_DOC = f"{AR}/MEERKAT_GAMES.md"

EXECUTORS = "Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors"
KIT = f"{EXECUTORS}/MeerkatSolitonKit.cs"
EXECUTOR = f"{EXECUTORS}/MeerkatSolitonExecutor.cs"
SWALLOW = f"{EXECUTORS}/MeerkatSwallow.cs"
PORTAL = "Assets/_Scripts/Controller/Vessel/R_VesselActions/MeerkatExitPortal.cs"
CONFIG = "Assets/_SO_Assets/VesselActions/Meerkat/MeerkatSolitonConfig.asset"
PREFAB = "Assets/_Prefabs/Spacevessels/Meerkat.prefab"
COMBAT_HIT_CHANNEL = "45fb43a0cddb5cc3cc4dc448df994152"   # Event_CombatHitStats.asset


def shared_metas(g):
    """The shared AI hook's script meta, its folder's and the shared doc's - emitted identically by all three."""
    g.script_meta(STEER, guid("script/MeerkatArcadeSteer"))
    g.folder_meta(SHARED_DIR)
    g.text_meta(SHARED_DOC)


def shared_minted():
    return [guid("script/MeerkatArcadeSteer"), guid(f"folder/{SHARED_DIR}"), guid(f"text/{SHARED_DOC}")]


def ladder_yaml(name, ladders):
    return f"  {name}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in ladders[name][0])


def ladder_errors(settings_cs, cls, ladders, shared):
    """Every ladder four rungs, monotone toward harder, and its C# initializer the authored values."""
    errors = []
    for name, (values, harder) in ladders.items():
        if len(values) != 4:
            errors.append(f"{cls}.{name}: needs four rungs")
        pairs = list(zip(values, values[1:]))
        if harder == "up" and any(b < a for a, b in pairs) or harder == "down" and any(b > a for a, b in pairs):
            errors.append(f"{cls}.{name}: not monotone toward harder ({values})")
        init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
        if f"{name} = {init};" not in settings_cs:
            errors.append(f"{cls}.{name}: the C# initializer does not match {init}")
    for field, value in shared.items():
        if f"{field} = {lib.num(float(value))}f;" not in settings_cs:
            errors.append(f"{cls}.{field} initializer differs from the authored {value}")
    return errors


INPUT_WRITES = ("PerformShipControllerActions", "InputStatus", "LeftTriggerAnalog", "RightTriggerAnalog",
                "PressForm", "ToggleSentinelMode", "SelectedIntensity")


def steer_errors(g, mode_files):
    """The AI only steers: neither the shared hook nor a mode's picker writes an input or reads the intensity, and the
    hook keys its rungs on the seat's AI level."""
    errors = []
    steer = g.read(STEER)
    if "AIDifficultyFor(" not in steer:
        errors.append("MeerkatArcadeSteer must key a seat's level on gameData.AIDifficultyFor(seat), not the intensity")
    for rel in [STEER] + list(mode_files):
        src = g.read(rel)
        for needle in INPUT_WRITES:
            if needle in src:
                errors.append(f"{os.path.basename(rel)} uses '{needle}' - the Meerkat games' AI only steers (the "
                              f"hull's own autopilot forms the solitons and turns Sentinel Mode on), and keys on the "
                              f"seat's AI level, never the intensity")
    # The hook rests on the hull planning its solitons toward the AIPilot's target.
    ex = g.read(EXECUTOR)
    for needle in ("ai.TargetPosition - hull.position", "PressForm();", "if (!config.AiWormhole || !ai || !hull) return;"):
        if needle not in ex:
            errors.append(f"MeerkatSolitonExecutor.Autopilot no longer has '{needle}' - the games' AI steers on the "
                          f"assumption the hull forms its solitons toward its AIPilot's target")
    if "  aiWormhole: 1\n" not in g.read(CONFIG):
        errors.append("MeerkatSolitonConfig.aiWormhole is off - an AI Meerkat would form no soliton in any game")
    return errors


def enum_errors(g, member, mode_id, hint, toast_id):
    errors = []
    if f"{member} = {mode_id}," not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
        errors.append(f"GameModes.{member} is not {mode_id} (GAME_KIT_ROSTER.md reserved it)")
    if f"{hint} = {toast_id}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
        errors.append(f"GameToastSituation.{hint} is not {toast_id} (GAME_KIT_ROSTER.md reserved it)")
    return errors


def scene_spawn_rings(scene):
    """The per-intensity spawn ring a committed scene authors (its ServerPlayerVesselInitializer block)."""
    m = re.search(r"^  spawnRingRadiusFloorByIntensity:\n((?:  - [0-9.]+\n)+)", scene, re.M)
    assert m, "spawnRingRadiusFloorByIntensity not found in the donor scene"
    return [v.strip()[2:] for v in m.group(1).splitlines()]


def _self_test():
    ok = True

    class G:
        def __init__(self, files):
            self.files = files

        def read(self, rel):
            return self.files[rel] if rel in self.files else lib.Generator(0, "x").read(rel)

    ladders = {"aiX": ([1, 2, 3, 4], "up")}
    if ladder_errors("aiX = { 1f, 2f, 3f, 4f };", "S", ladders, {}):
        ok = False; print("FAIL: a matching ladder reported errors")
    if not ladder_errors("aiX = { 1f, 2f, 3f, 5f };", "S", ladders, {}):
        ok = False; print("FAIL: a drifted initializer passed")
    if not ladder_errors("aiX = { 4f, 3f, 2f, 1f };", "S", {"aiX": ([4, 3, 2, 1], "up")}, {}):
        ok = False; print("FAIL: a ladder running the wrong way passed")
    bad = G({STEER: "AIDifficultyFor( input.InputStatus.LeftTriggerAnalog = 1;"})
    if not steer_errors(bad, []):
        ok = False; print("FAIL: a hook that writes an input passed")
    if steer_errors(G({}), []):
        ok = False; print("FAIL: the shipped hook reported errors:", steer_errors(G({}), []))
    if scene_spawn_rings("  spawnRingRadiusFloorByIntensity:\n  - 1\n  - 2\n  spawnFormation: 0\n") != ["1", "2"]:
        ok = False; print("FAIL: spawn rings misread")
    print("meerkat_games_common self-test:", "OK" if ok else "FAILED")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(_self_test())
