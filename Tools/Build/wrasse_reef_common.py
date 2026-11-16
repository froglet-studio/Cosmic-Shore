"""
The shared half of the Wrasse and Grouper's three reef games' generators (author_cleaner_ram_assets.py,
author_reefbuild_assets.py, author_blastline_assets.py; Assets/_Scripts/Controller/Arcade/WRASSEREEF.md).

The three games share one controller base, one settings type, the crystal scatter and the AI Grouper guide
(Arcade/WrasseReef/). Each generator emits those scripts' .meta files identically (same guid, same text), so any one of
them can run alone and --check stays green on all three. Each game's own numbers, card, toasts, preview and scene are
its generator's.

THE IDS ARE THE ROSTER'S, never "the next free id": Docs/ElementalLoop/GAME_KIT_ROSTER.md rows 21-23 reserved GameModes
111-113 and hint toasts 186-188 on 2026-11-15, while parallel sessions build the other vessels' missing games.
"""
import os
import re

import arcade_mode_lib as lib

guid = lib.guid

AR = "Assets/_Scripts/Controller/Arcade"
REEF_DIR = f"{AR}/WrasseReef"
DOC = f"{AR}/WRASSEREEF.md"

SHARED_SCRIPTS = {
    "WrasseReefSettingsSO":     f"{REEF_DIR}/WrasseReefSettingsSO.cs",
    "WrasseReefCrystals":       f"{REEF_DIR}/WrasseReefCrystals.cs",
    "WrasseGrouperGuide":       f"{REEF_DIR}/WrasseGrouperGuide.cs",
    "WrasseReefControllerBase": f"{REEF_DIR}/WrasseReefControllerBase.cs",
}
SHARED_GUIDS = {k: guid(f"script/{k}") for k in SHARED_SCRIPTS}
SHARED_MINTED = list(SHARED_GUIDS.values()) + [guid(f"folder/{REEF_DIR}"), guid(f"text/{DOC}")]

WRASSE_ID = lib.VESSEL_CLASS_ID["Wrasse"]
VESSEL_WRASSE = lib.VESSELS["Wrasse"]

KITE_CONFIG = "Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset"
KITE_NAVIGATOR = "Assets/_Scripts/Controller/Vessel/Crew/KiteNavigator.cs"
SETTINGS_CS = SHARED_SCRIPTS["WrasseReefSettingsSO"]
GUIDE_CS = SHARED_SCRIPTS["WrasseGrouperGuide"]

BARREN_CELL = "Assets/_SO_Assets/Cell Configs/Barren Cell/Barren Cell Config.asset"
ELEMENT = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}

# The AI Grouper's ladders (index = AI difficulty - 1; Easy, Medium, Hard, Legendary) and which way is harder.
LADDERS = {
    "aiRetargetSeconds": ([3, 2, 1.4, 1], "down"),
    "aiCrystalReach":    ([250, 350, 450, 550], "up"),
    "aiHuntRange":       ([450, 600, 750, 900], "up"),
    "aiLeadSeconds":     ([0, 0.2, 0.35, 0.5], "up"),
}
SHARED_AI = {"aiOrbitFraction": 0.55, "aiOrbitDegreesPerSecond": 6, "aiRaidDeficitFraction": 0.15,
             "aiLateRaidSeconds": 20}


def emit_shared(g):
    """The four shared scripts' metas, the folder and the doc."""
    for k, p in SHARED_SCRIPTS.items():
        g.script_meta(p, SHARED_GUIDS[k])
    g.folder_meta(REEF_DIR)
    g.text_meta(DOC)


def kite(field):
    """A number off the shipped KiteNavigatorConfig.asset (the Wrasse's seat), so a seat retune moves the models."""
    m = re.search(rf"^  {field}: ([-0-9.]+)$", lib_read(KITE_CONFIG), re.M)
    assert m, f"KiteNavigatorConfig.{field} not found"
    return float(m.group(1))


def kite_vec(field):
    m = re.search(rf"^  {field}: \{{x: ([0-9.]+), y: ([0-9.]+), z: ([0-9.]+)\}}$", lib_read(KITE_CONFIG), re.M)
    assert m, f"KiteNavigatorConfig.{field} not found"
    return tuple(float(v) for v in m.groups())


def lib_read(rel):
    with open(os.path.join(lib.ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


def settings_yaml(script_guid, name, element, share, crystals, radius, seed, regrow):
    """A WrasseReefSettings asset."""
    out = lib.header_for(script_guid, name)
    out += f"  favouredElement: {ELEMENT[element]}\n  favouredShare: {lib.num(float(share))}\n"
    out += "  crystalCountByIntensity:\n" + "".join(f"  - {n}\n" for n in crystals)
    out += (f"  crystalScatterRadius: {lib.num(float(radius))}\n  crystalScatterSeed: {seed}\n"
            f"  crystalRegrowSeconds: {lib.num(float(regrow))}\n"
            "  progressSampleSeconds: 1\n  leadAnnounceAfterSeconds: 20\n  leadAnnounceMinGapSeconds: 10\n")
    for n, (values, _) in LADDERS.items():
        out += f"  {n}:\n" + "".join(f"  - {lib.num(float(v))}\n" for v in values)
    for n, v in SHARED_AI.items():
        out += f"  {n}: {lib.num(float(v))}\n"
    return out


def shared_errors(g, mode_name, mode_id, hint_name, hint_id, crystals):
    """The checks every reef game makes: its ids are the roster's, the ladders and the C# initializers agree, the kite
    still reads the hull's element levels for its motes (the game's whole lever), and the guide never presses."""
    errors = []
    if f"        {mode_name} = {mode_id},\n" not in g.read("Assets/_Scripts/Data/Enums/GameModes.cs"):
        errors.append(f"GameModes.{mode_name} is not {mode_id} (GAME_KIT_ROSTER.md reserved it)")
    if f"        {hint_name} = {hint_id}," not in g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs"):
        errors.append(f"GameToastSituation.{hint_name} is not {hint_id} (GAME_KIT_ROSTER.md reserved it)")
    if len(crystals) != 4 or any(b > a for a, b in zip(crystals, crystals[1:])) or crystals[-1] < 1:
        errors.append(f"{mode_name}: the crystal ladder must be four rungs, non-increasing, never empty ({crystals})")

    cs = g.read(SETTINGS_CS)
    for n, (values, harder) in LADDERS.items():
        pairs = list(zip(values, values[1:]))
        if len(values) != 4 or (harder == "up" and any(b < a for a, b in pairs)) or \
                (harder == "down" and any(b > a for a, b in pairs)):
            errors.append(f"{n}: four rungs, monotone toward harder ({values})")
        init = "{ " + ", ".join(lib.num(float(v)) + "f" for v in values) + " }"
        if f"{n} = {init};" not in cs:
            errors.append(f"WrasseReefSettingsSO.{n}: the C# initializer does not match {init}")
    for n, v in SHARED_AI.items():
        if f"{n} = {lib.num(float(v))}f;" not in cs:
            errors.append(f"WrasseReefSettingsSO.{n} initializer differs from the authored {v}")

    navigator = g.read(KITE_NAVIGATOR)
    for needle in ("_cfg.MoteBaseWeight + _cfg.MoteWeightPerLevel * (rs ? rs.GetLevel((Element)(e + 1)) : 0)",
                   "CombatHitClass.Strike", "prism.Initialize(_status.PlayerName);",
                   "AnnonymousExplosion = false,", "p.Prism.Damage(impact, own, name);", "public int Charges =>"):
        if needle not in navigator:
            errors.append(f"KiteNavigator no longer has '{needle}' - the reef games' crystal lever, ram score, cluster "
                          f"volume, blast credit or AI read rests on it; re-check the seat")
    guide = g.read(GUIDE_CS)
    if "SelectedIntensity" in guide or "AIDifficultyFor(" not in guide:
        errors.append("WrasseGrouperGuide must key its ladders on gameData.AIDifficultyFor(seat), not the intensity")
    for banned in ("PerformShipControllerActions", "InputStatus", "TriggerAnalog", "ElementWeight"):
        if banned in guide:
            errors.append(f"WrasseGrouperGuide touches '{banned}' - it only steers the Grouper; the kite is its own AI's")
    return errors


def comeback_error(target, rate, what):
    if 0.25 * target * rate < 1.0:
        return [f"comeback rate {rate} is dead against {what} {target}: a quarter-of-target deficit buys "
                f"{0.25 * target * rate:.2f} element levels (< 1)"]
    return []


def scene_identity_errors(scene, present, absent, ai_count=4):
    errors = []
    if scene is None:
        return ["no scene"]
    for name, gd in present.items():
        if gd not in scene:
            errors.append(f"scene missing {name}")
    for name, gd in absent.items():
        if gd in scene:
            errors.append(f"scene still references the donor's {name}")
    if scene.count(f"  - vesselClass: {WRASSE_ID}\n") != ai_count:
        errors.append(f"scene does not carry exactly {ai_count} Wrasse AI templates")
    return errors
