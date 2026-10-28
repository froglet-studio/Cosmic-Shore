#!/usr/bin/env python3
"""Author every asset of SHIVA'S CELL - GameModes.Tandava, redesigned (Shombith, 2026-10-28; Assets/_Scripts/Controller/
Arcade/TANDAVA.md §0; the lab is Docs/Studios/ShivaLab, https://claude.ai/artifact/HGt5NXLXzCCfEVDRsQ6e3G).

    python3 Tools/Build/author_shiva_assets.py            # write
    python3 Tools/Build/author_shiva_assets.py --check    # fail if anything on disk differs from what this would write

A voyage location: the squad goes into a barren cell, planets wheeling round it, to break a humongous Shiva inside three
shells of danger prisms by threading his ring circuits; he claps, the cell collapses and grows again, the swarm comes out
of his hands. The LAB is the source of truth for the numbers: this script reads its SHIPPED table (node, sim.js) and
writes it into ShivaSettings.asset, and the swarm_core_harness (mode `shiva`) holds the director to the lab's own code.

It writes, each a file it owns:
  * the scripts' .metas (ShivaController, ShivaSettingsSO, ShivaDirectorCore, ShivaFigure, ShivaScoringRuleSO,
    SpawnableShivaOrrery);
  * the cell: Shiva Cell Config (no nucleus - he IS the middle; a 1,600 u membrane; the planets), its spawn profile
    (barren: the controller grows each form's flora and lets out each form's swarm), ShivaMembrane.prefab and
    SpawnableShivaOrrery.prefab (copies of the shipped CapsuleMembrane and SpawnableOrrery prefabs, re-derived each run);
  * ShivaSettings.asset (the director, the three forms, the serpents' plan indices - read out of Tandava's committed swarm
    config, the shield, the figure, the text), ShivaScoringRule.asset;
  * the card ArcadeGameTandavaShiva.asset (parked in Games/_Archive like every voyage; on the master roster only), its
    toasts and its preview;
  * MinigameTandavaShiva.unity - Tandava's scene with the controller, settings, rule and cell swapped (Tandava's own
    scene is untouched: it is Tandava: Severing's donor).
And it registers the card (master roster), the toasts, the preview and the build scene. author_tandava_assets.py, with
SUPERSEDED, takes the first design's card, toasts and preview out of the same registries.
"""
import json
import os
import re
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib          # noqa: E402

ROOT = lib.ROOT
num = lib.num
guid = lib.guid
errors = []


def enum_value(enum_file, member):
    m = re.search(rf"\b{member}\s*=\s*(\d+)\s*,", open(os.path.join(ROOT, enum_file), encoding="utf-8").read())
    assert m, f"{member} is not in {enum_file}"
    return int(m.group(1))


GAME_MODES_CS = "Assets/_Scripts/Data/Enums/GameModes.cs"
TOASTS_CS = "Assets/_Scripts/Data/Enums/GameToastSituation.cs"
MODE_ID = enum_value(GAME_MODES_CS, "Tandava")
# Shiva's narration rides the mode's own toast situations (they are GameModes.Tandava's): no new enum ids to collide
TOAST = {k: enum_value(TOASTS_CS, f"Tandava{k}") for k in
         ("MatchStart", "FormTaken", "Completed", "Broken", "DenyHint", "Rising", "HaloBroken", "Lunge")}
g = lib.Generator(MODE_ID, "TandavaShiva")

# ── the lab's numbers (the source of truth) ─────────────────────────────────────
LAB_SIM = os.path.join(ROOT, "Docs/Studios/ShivaLab/sim.js")
try:
    LAB = json.loads(subprocess.run(["node", "-e", f"require({json.dumps(LAB_SIM)}); process.stdout.write(JSON.stringify(ShivaSim.SHIPPED))"],
                                    check=True, capture_output=True, text=True).stdout)
except (OSError, subprocess.CalledProcessError) as e:
    sys.exit(f"cannot read the lab's SHIPPED table with node ({e}) - this script needs node")

# ── scripts ─────────────────────────────────────────────────────────────────────
MODE_DIR = "Assets/_Scripts/Controller/Arcade/Tandava"
SCRIPT_PATHS = {
    "ShivaController": f"{MODE_DIR}/ShivaController.cs",
    "ShivaSettingsSO": f"{MODE_DIR}/ShivaSettingsSO.cs",
    "ShivaDirectorCore": f"{MODE_DIR}/ShivaDirectorCore.cs",
    "ShivaFigure": f"{MODE_DIR}/ShivaFigure.cs",
    "ShivaScoringRuleSO": "Assets/_Scripts/Controller/Arcade/Scoring/ShivaScoringRuleSO.cs",
    "SpawnableShivaOrrery": "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableShivaOrrery.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}
for k, rel in SCRIPT_PATHS.items():
    g.script_meta(rel, G_SCRIPT[k])

# ── what this script owns ───────────────────────────────────────────────────────
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Shiva Cell"
SETTINGS_REL = "Assets/_SO_Assets/Games/ShivaSettings.asset"
RULE_REL = "Assets/_SO_Assets/Scoring Rules/ShivaScoringRule.asset"
CARD_REL = "Assets/_SO_Assets/Games/_Archive/ArcadeGameTandavaShiva.asset"
TOASTS_REL = "Assets/_SO_Assets/Game Toasts/GameToastConfig_TandavaShiva.asset"
PREVIEW_REL = "Assets/_SO_Assets/Mode Previews/ModePreview_TandavaShiva.asset"
MEMBRANE_REL = "Assets/_Prefabs/Environment/ShivaMembrane.prefab"
ORRERY_REL = "Assets/_Prefabs/Spawnables/SpawnableShivaOrrery.prefab"
SCENE_NAME = "MinigameTandavaShiva"
G_ASSET = {k: guid(f"asset/Shiva/{k}") for k in (
    "CellConfig", "SpawnProfile", "Settings", "Rule", "Card", "Toasts", "Preview", "Membrane.prefab", "Orrery.prefab", "Scene")}
g.folder_meta(CELL_DIR)

# ── what it reads (existing guids, never invented) ──────────────────────────────
E = lambda rel: lib.existing_guid(rel)
T = dict(
    controller=E(f"{MODE_DIR}/TandavaController.cs"), settings=E("Assets/_SO_Assets/Games/TandavaSettings.asset"),
    rule=E("Assets/_SO_Assets/Scoring Rules/TandavaScoringRule.asset"),
    cell=E("Assets/_SO_Assets/Cell Configs/Tandava Cell/Tandava Cell Config.asset"),
    swarm=E("Assets/_SO_Assets/Swarm Fauna/Tandava/TandavaSwarmFaunaConfig.asset"),
    species=E("Assets/_SO_Assets/Cell Configs/Tandava Cell/Tandava Swarm Fauna Config Data.asset"),
)
TANDAVA_SCENE = f"{lib.SCENES_DIR}/MinigameTandava.unity"
CAPSULE_MEMBRANE = "Assets/_Prefabs/Environment/CapsuleMembrane.prefab"
ORRERY_PREFAB = "Assets/_Prefabs/Spawnables/SpawnableOrrery.prefab"
ORRERY_SCRIPT = E("Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableOrrery.cs")
LIFE = "Assets/_SO_Assets/Lifeforms"
SPECIES = {   # the swarm's other creatures and the regrown cell's colonies: the canonical species, by name
    "Shark Mass": f"{LIFE}/Shark Fauna Mass.asset", "Shark Charge": f"{LIFE}/Shark Fauna Charge.asset",
    "Piranha": "Assets/_SO_Assets/Cell Configs/Astro League Cell/Astro League Piranha Fauna Config Data.asset",
    "Clawfish Time": f"{LIFE}/Clawfish Fauna Time.asset", "Clawfish Space": f"{LIFE}/Clawfish Fauna Space.asset",
    "Gyroid Mass": f"{LIFE}/Gyroid Flora Mass.asset", "Gyroid Space": f"{LIFE}/Gyroid Flora Space.asset",
    "SchwarzP Mass": f"{LIFE}/SchwarzP Flora Mass.asset", "SchwarzP Time": f"{LIFE}/SchwarzP Flora Time.asset",
    "Nested Charge": f"{LIFE}/Nested Gyroid Flora Charge.asset", "Nested Space": f"{LIFE}/Nested Gyroid Flora Space.asset",
}
EXISTING = {f"species/{k}": E(v) for k, v in SPECIES.items()}
EXISTING.update({f"tandava/{k}": v for k, v in T.items()})
EXISTING.update(lib.CARD_ART)
EXISTING["CardBackground"] = E("Assets/_Graphics/ARCADE/CardBackgrounds/Tandava.png")
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
EXISTING["Cytoplasm"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["ExtraOmniCrystals"] = "daa37ae0e7af4b04383c1c4e6e76817d"   # the Swarm cell's modifier (Tandava's too)
EXISTING["PrismMesh"] = "8f4a329b97818584886f68265bb01988"            # SpawnablePrism.prefab's mesh
EXISTING["PrismMaterial"] = E("Assets/_Graphics/Materials/BlockMaterials/BlueBlockMateral.mat")
EXISTING["PrismChannel"] = E("Assets/_SO_Assets/Event Channels/Prisms/EventOnSpawnPrismAndReturn.asset")
EXISTING["Orrery"] = ORRERY_SCRIPT
HULLS = ("Squirrel", "Sparrow", "Rhino", "Manta", "Dolphin", "Urchin", "Serpent", "Scarab", "Stoat")   # the lab's nine
for h in HULLS:
    EXISTING[f"Vessel_{h}"] = lib.VESSELS[h]
for k, v in lib.SCRIPT_GUIDS.items():
    EXISTING[f"script/{k}"] = v


def so(script, name):
    return lib.header_for(script, name)


def yaml_str(s):
    if re.search(r"[:#'\"{}\[\],&*!|>%@`]", s) or s != s.strip() or not s:
        return "'" + s.replace("'", "''") + "'"
    return s


def colour(c):
    return "{r: %s, g: %s, b: %s, a: %s}" % tuple(num(float(v)) for v in c)


def ref(g_, file_id=11400000, kind=2):
    return f"{{fileID: {file_id}, guid: {g_}, type: {kind}}}"


def set_key(text, key, value, label):
    text, n = re.subn(rf"(?m)^  {key}: .*$", f"  {key}: {value}", text)
    assert n == 1, f"{label}: '{key}' found {n} times"
    return text


# ═════════════════════════════════════════════════════════════════════════════
# 1. The serpents' plans: their indices in Tandava's committed swarm config
# ═════════════════════════════════════════════════════════════════════════════
SWARM_CFG = g.read("Assets/_SO_Assets/Swarm Fauna/Tandava/TandavaSwarmFaunaConfig.asset")
m = re.search(r"(?m)^  ScriptedPlans:\n((?:  - [^\n]*\n)+)", SWARM_CFG)
assert m, "Tandava's swarm config has no ScriptedPlans"
plan_guids = re.findall(r"guid: ([0-9a-f]{32})", m.group(1))
by_guid = {}
for fn in os.listdir(os.path.join(ROOT, "Assets/_SO_Assets/Swarm Fauna/Tandava")):
    if fn.startswith("SwarmPlan_tandava_") and fn.endswith(".json.meta"):
        gg = re.search(r"^guid: ([0-9a-f]{32})", open(os.path.join(ROOT, "Assets/_SO_Assets/Swarm Fauna/Tandava", fn)).read(), re.M).group(1)
        by_guid[gg] = fn[len("SwarmPlan_tandava_"):-len(".json.meta")]
PLAN_INDEX = {by_guid.get(gg, "?"): i for i, gg in enumerate(plan_guids)}
for heads in (5, 7, 10):
    for pose in ("", "_rear", "_strike"):
        if f"many_headed_{heads}{pose}" not in PLAN_INDEX:
            errors.append(f"Tandava's swarm config has no many_headed_{heads}{pose} plan")


def plans(heads):
    return {k: PLAN_INDEX.get(f"many_headed_{heads}{pose}", -1) for k, pose in (("body", ""), ("rear", "_rear"), ("strike", "_strike"))}


# ═════════════════════════════════════════════════════════════════════════════
# 2. The cell: barren, no nucleus (he is the middle), a 1,600 u membrane, the planets
# ═════════════════════════════════════════════════════════════════════════════
CELL_RADIUS = LAB["cellRadius"]
mem = g.read(CAPSULE_MEMBRANE)
mem, n = re.subn(r"(?m)^  m_Name: (?!$).*$", "  m_Name: ShivaMembrane", mem, count=1)
assert n == 1, "membrane: root name"
mem = set_key(mem, "radius", num(CELL_RADIUS), "membrane")
mem = set_key(mem, "animationPreset", "{fileID: 0}", "membrane")   # the baked animation is keyed by radius: it animates live
g.emit_prefab(MEMBRANE_REL, G_ASSET["Membrane.prefab"], mem)
MEMBRANE_ROOT_FID = re.search(r"--- !u!1 &(\d+)\n", mem).group(1)

orr = g.read(ORRERY_PREFAB)
orr = lib.swap_guid(orr, ORRERY_SCRIPT, G_SCRIPT["SpawnableShivaOrrery"], "planets script")
orr, n = re.subn(r"(?m)^  m_Name: SpawnableOrrery$", "  m_Name: SpawnableShivaOrrery", orr)
assert n == 1, "planets: root name"
orr = set_key(orr, "seed", "69", "planets")
g.emit_prefab(ORRERY_REL, G_ASSET["Orrery.prefab"], orr)
ORRERY_CFG = g.read("Assets/_SO_Assets/Cell Configs/Orrery Cell/Orrery Cell Config.asset")
ORRERY_FID = re.search(r"(?m)^  EnvironmentPrefab: \{fileID: (\d+),", ORRERY_CFG).group(1)

g.emit_asset(f"{CELL_DIR}/Shiva Cell Spawn Profile.asset", G_ASSET["SpawnProfile"],
             so(lib.SCRIPT_GUIDS["SpawnProfileSO"], "Shiva Cell Spawn Profile") +
             "  FloraExcludeLocalDomain: 0\n  FloraSpawnVolumeCeiling: 12000\n  FloraInitialDelaySeconds: 0\n"
             "  FloraSpawnIntervalSeconds: 0\n  FloraPopulationScale: 1\n  FloraPlantBudgetScale: 1\n"
             "  FloraPrismScale: 1\n  SupportedFloras: []\n"
             "  FaunaExcludeLocalDomain: 0\n  InitialFaunaSpawnWaitTime: 6\n  InitialFaunaReleaseTier: 2147483647\n"
             "  FaunaSpawnVolumeThreshold: 1\n  FaunaPopulationScale: 1\n  BaseFaunaSpawnTime: 30\n"
             "  SeedFullWaveEveryTick: 0\n  FaunaFoodFloor: 0\n  FaunaInitialDelaySeconds: 0\n"
             "  FaunaSpawnIntervalSeconds: 0\n  HerbivoreSpawnPointCount: 3\n  HerbivoreSpawnRadius: 500\n"
             "  PredatorSpawnPointCount: 0\n  PredatorSpawnRadius: 900\n  SupportedFaunas: []\n")

g.emit_asset(f"{CELL_DIR}/Shiva Cell Config.asset", G_ASSET["CellConfig"],
             so(lib.SCRIPT_GUIDS["CellConfigDataSO"], "Shiva Cell Config") + f"""  CellName: Shiva's Cell
  Description: A barren cell for the god at its middle - no nucleus, a membrane twice Tandava's, his
    planets wheeling round him. The controller grows each form's flora and lets out each form's swarm.
    Authored by Tools/Build/author_shiva_assets.py
  Icon: {ref(EXISTING['CellIcon'], 21300000, 3)}
  Difficulty: 3
  CellEndGameScore: 0
  MembranePrefab: {ref(G_ASSET['Membrane.prefab'], MEMBRANE_ROOT_FID, 3)}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {ref(EXISTING['Cytoplasm'], 639495419069806261, 3)}
  CytoplasmShardDistance: 0
  CellModifiers:
  - {ref(EXISTING['ExtraOmniCrystals'], 8058406376250941529, 3)}
  SpawnProfile: {ref(G_ASSET['SpawnProfile'])}
  BootDefault: 0
  EnvironmentPrefab: {ref(G_ASSET['Orrery.prefab'], ORRERY_FID, 3)}
  EnvironmentIntensity: 1
  SenseRadiusOverride: 0
  PetalBurnRule: 0
  initialControllingDomain: 1
  conservedFaunaStomach: 0
  PhaseThresholds:
    RestlessEnter: 3000
    RestlessExit: 2400
    FrenzyEnter: 16000
    FrenzyExit: 14000
    RestlessEnterVolume: 90000
    RestlessExitVolume: 70000
    FrenzyEnterVolume: 600000
    FrenzyExitVolume: 520000
""")

# ═════════════════════════════════════════════════════════════════════════════
# 3. The mode: settings (from the lab), rule, card, toasts, preview
# ═════════════════════════════════════════════════════════════════════════════
DIRECTOR = [   # ShivaDirectorSettings field <- the lab's SHIPPED key (or a constant the lab hard-codes)
    ("CellRadius", LAB["cellRadius"]), ("Mouth", LAB["mouth"]),
    ("RingsStill", LAB["ringsP1"]), ("RingsNataraja", LAB["ringsP2"]), ("RingsMouth", LAB["ringsP3"]),
    ("RingOuter", LAB["ringOuter"]), ("RingStepDegrees", LAB["ringStepDeg"]), ("RingRise", LAB["ringRise"]), ("RingInset", 85),
    ("DanceSpeed", LAB["danceSpeed"]), ("DanceBob", LAB["danceBob"]), ("DanceSway", LAB["danceSway"]),
    ("ClockStill", LAB["clockP1"]), ("ClockNataraja", LAB["clockP2"]), ("ClockMouth", LAB["clockP3"]),
    ("ShellOuter", LAB["shell1"]), ("ShellMiddle", LAB["shell2"]), ("ShellInner", LAB["shell3"]),
    ("WaveSpeed", LAB["waveSpeed"]), ("ClapReach", LAB["clapReach"]), ("RegrowSeconds", LAB["regrowSeconds"]),
    ("ClapSeconds", 1.2), ("HandsMeetSeconds", 0.6), ("WaveStartRadius", 160), ("MouthOpenRate", 0.8), ("TurnRate", 0.12),
]
INT_FIELDS = {"RingsStill", "RingsNataraja", "RingsMouth"}
director_yaml = "  Director:\n" + "".join(f"    {k}: {int(v) if k in INT_FIELDS else num(v)}\n" for k, v in DIRECTOR)


def pal(bright, dull, edge, straight, cyto):
    return "".join(f"      {k}: {colour(c)}\n" for k, c in (("MembraneBright", bright), ("MembraneDull", dull), ("NucleusEdge", edge),
                                                            ("NucleusStraight", straight), ("Cytoplasm", cyto)))


FORMS = [
    dict(name="The Still Shiva", line="", heads=5, serpents=LAB["serpentsP1"],
         fauna=[("Shark Mass", LAB["sharksP1"])], flora=[("Gyroid Mass", 6), ("Gyroid Space", 4)],
         palette=pal((0.42, 0.5, 1.5, 1), (0.03, 0.04, 0.22, 1), (0.3, 0.4, 1.0, 0.4), (0.4, 0.5, 1.1, 0.4), (0.32, 0.36, 0.7, 1))),
    dict(name="Nataraja", line="Nataraja dances. His rings will not keep still.", heads=7, serpents=LAB["serpentsP2"],
         fauna=[("Shark Mass", LAB["sharksP2"]), ("Piranha", LAB["piranhaP2"]), ("Clawfish Time", LAB["clawfishP2"])],
         flora=[("SchwarzP Mass", 6), ("SchwarzP Time", 4)],
         palette=pal((1.4, 1.05, 0.45, 1), (0.16, 0.08, 0.2, 1), (1.0, 0.75, 0.35, 0.45), (1.1, 0.9, 0.5, 0.45), (0.8, 0.55, 0.75, 1))),
    dict(name="The Devouring Mouth", line="His mouth opens on a black hole. Do not get drawn in.", heads=10, serpents=LAB["serpentsP3"],
         fauna=[("Shark Charge", LAB["sharksP3"]), ("Piranha", LAB["piranhaP3"]), ("Clawfish Space", LAB["clawfishP3"])],
         flora=[("Nested Charge", 5), ("Nested Space", 5)],
         palette=pal((1.5, 0.3, 0.55, 1), (0.1, 0.0, 0.04, 1), (1.0, 0.25, 0.4, 0.45), (1.1, 0.35, 0.5, 0.45), (0.6, 0.2, 0.35, 1))),
]
forms_yaml = "  Forms:\n"
for f in FORMS:
    p = plans(f["heads"])
    forms_yaml += (f"  - DisplayName: {yaml_str(f['name'])}\n    BeginLine: {yaml_str(f['line']) if f['line'] else ''}\n"
                   f"    SerpentCount: {int(f['serpents'])}\n    SerpentPlanIndex: {p['body']}\n    RearPlanIndex: {p['rear']}\n"
                   f"    StrikePlanIndex: {p['strike']}\n    Fauna:\n" +
                   "".join(f"    - Config: {ref(EXISTING['species/' + s])}\n      Count: {int(c)}\n" for s, c in f["fauna"]) +
                   "    Flora:\n" + "".join(f"    - Config: {ref(EXISTING['species/' + s])}\n      Count: {int(c)}\n" for s, c in f["flora"]) +
                   f"    Crystals: {int(LAB['crystals'])}\n    Palette:\n" + f["palette"])

g.emit_asset(SETTINGS_REL, G_ASSET["Settings"], so(G_SCRIPT["ShivaSettingsSO"], "ShivaSettings") + director_yaml + forms_yaml + f"""  SwarmConfig: {ref(T['swarm'])}
  SerpentSpecies: {ref(T['species'])}
  GuardCruise: 1
  GuardTurn: 1.6
  StrikeCruise: {num(round(LAB['serpentLunge'] / LAB['serpentCruise'], 3))}
  StrikeTurn: 3
  LungeRange: {num(LAB['lungeRange'])}
  LungeCooldown: {num(LAB['lungeCooldown'])}
  RearSeconds: 0.6
  StrikeSeconds: 2.5
  GuardOrbit: 1.4
  NudgeThreshold: 25
  NudgeFraction: 0.2
  MaxNudge: 8
  ShellSpacing: 70
  ShellPrismScale: {{x: 10, y: 10, z: 2}}
  ShellDomain: 3
  PrismChannel: {ref(EXISTING['PrismChannel'])}
  FigureMesh: {ref(EXISTING['PrismMesh'], 4300000, 2)}
  FigureMaterial: {ref(EXISTING['PrismMaterial'], 2100000, 2)}
  FigureSpacing: 14
  BodyBright: {colour((0.32, 0.5, 1.6, 1))}
  BodyDark: {colour((0.02, 0.04, 0.3, 1))}
  EyeColor: {colour((2.2, 2.2, 2.6, 1))}
  ThirdEyeColor: {colour((2.6, 1.6, 0.4, 1))}
  VasukiColor: {colour((0.15, 0.9, 0.6, 1))}
  HairColor: {colour((0.12, 0.1, 0.32, 1))}
  MoonColor: {colour((2, 1.85, 1.3, 1))}
  WaveShards: 240
  StripSpeed: 40
  TransitionFlash: 0.6
  TransitionSeconds: 2
  RestoreSeconds: 3
  BlackHoleStrength: {num(LAB['bhStrength'])}
  WinShards: 500
  BurstSpeed: 260
  BurstScale: 9
  FlashRadius: 1800
  FlashStrength: 1
  FlashSeconds: 4
  MaxPlausibleSpeed: 1600
  StartLine: Thread his rings. Break his shield.
  ShellLine: {yaml_str('A shell breaks. {left} to go.')}
  ClapLine: Shiva claps.
  SwarmLine: His swarm comes out of his hands.
  WonLine: The Tandava is broken. The cell is yours.
  LostLine: The drum stops. His dance ends the cell.
  FormLabel: {yaml_str('Form {form} of {forms}')}
  RingsLabel: Rings
  DrumLabel: Drum
  ShellsLabel: Shells
  RegrowLabel: The cell grows again
""")

g.emit_asset(RULE_REL, G_ASSET["Rule"], so(G_SCRIPT["ShivaScoringRuleSO"], "ShivaScoringRule") +
             "  metric: 7\n  golfRules: 0\n  ringPoints: 10\n  crystalPoints: 1\n  victoryTitle: VICTORY\n  defeatTitle: DEFEAT\n"
             "  wonLabel: THE TANDAVA IS BROKEN\n  lostLabel: THE DRUM STOPS IN {form}\n"
             f"  rowDetail: {yaml_str('{rings} rings, {crystals} crystals')}\n")

g.emit_asset(CARD_REL, G_ASSET["Card"], so(EXISTING["script/SO_ArcadeGame"], "ArcadeGameTandavaShiva") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Tandava
  Description: Shiva sits in the middle of his cell inside three shells of danger prisms, and nothing can
    touch him. Thread the rings that spiral in to each shell before the drum stops, and the shell breaks.
    He claps - your prisms are wiped and your elements ripped away - and he changes. Break the third
    shell and the god comes apart in light. Your squad flies together.
  IconActive: {ref(EXISTING['IconActive'], 21300000, 3)}
  IconInactive: {ref(EXISTING['IconInactive'], 21300000, 3)}
  CardBackground: {ref(EXISTING['CardBackground'], 21300000, 3)}
  GolfScoring: 0
  SceneName: {SCENE_NAME}
  Vessels:
""" + "".join(f"  - {ref(EXISTING[f'Vessel_{h}'])}\n" for h in HULLS) + """  MinPlayersAllowed: 1
  MaxPlayersAllowed: 3
  MinDomainsAllowed: 1
  MaxDomainsAllowed: 1
  MinIntensity: 1
  MaxIntensity: 4
  ArenaRules: 1
  Tips:
  - Thread the lit ring. Any pilot in the squad can - the circuit moves on for everyone.
  - The last ring of each circuit faces him. Thread it and his shield cracks.
  - When he claps, get away from him. The wave rips every element off a vessel near him.
  - The serpents hold the rings ahead and lunge when you come close. Their danger plates sting.
  - Nataraja's rings dance. In his third form a black hole opens in his mouth - lean away from it.
  - The goals on the top left show his form, the rings left, the drum and his shells.
  ViewUserAction: 0
  PlayUserAction: 0
  StartingElements: []
  ComebackRatePerScoreDeficit: 0
""")

g.emit_asset(TOASTS_REL, G_ASSET["Toasts"], so(lib.SCRIPT_GUIDS["GameToastConfigSO"], "GameToastConfig_TandavaShiva") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             "".join(lib.toast(TOAST[k], "{0}", domain_names=0) for k in ("MatchStart", "FormTaken", "Completed", "Broken")) +
             lib.toast(TOAST["DenyHint"], "Thread the lit ring - it is the only way in", domain_names=0, idle=1, idle_seconds=40) +
             "".join(lib.toast(TOAST[k], "{0}", domain_names=0) for k in ("Rising", "HaloBroken", "Lunge")))
g.register_toast_config(G_ASSET["Toasts"])

g.emit_asset(PREVIEW_REL, G_ASSET["Preview"], so(lib.SCRIPT_GUIDS["ModePreviewDefinitionSO"], "ModePreview_TandavaShiva") + f"""  Mode: {MODE_ID}
  Notes: 'Shiva''s Cell as a preview flies it - the barren cell and his planets. No director runs in a preview,
    so neither he nor his rings are there. Vessel is -1 (ANY): the carousel''s pick flies it.'
  PreviewCell: {ref(G_ASSET['CellConfig'])}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  Vessel: -1
  ObjectiveText: Thread his rings and break his shield
  ObjectiveMetric: 7
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 0
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints:
""" + "".join(f"  - position: {{x: 0, y: 0, z: {z}}}\n    rotation: {{x: 0, y: -0.70710677, z: 0, w: 0.70710677}}\n" for z in (1320, 1380, 1440)))
g.register_preview(G_ASSET["Preview"])

# ═════════════════════════════════════════════════════════════════════════════
# 4. The scene: Tandava's, wired to Shiva (Tandava's own stays Tandava: Severing's donor)
# ═════════════════════════════════════════════════════════════════════════════
SPAWN_FIDS = ["1468661147", "1074736317", "1323644424", "1564881929"]


def move_transform(text, fid, pos, rot):
    mm = re.search(rf"--- !u!4 &{fid}\n(?:(?!--- !u!).*\n)*", text)
    assert mm, f"spawn transform {fid} not found"
    doc = mm.group(0)
    new = re.sub(r"m_LocalRotation: \{[^}]*\}", "m_LocalRotation: {x: %s, y: %s, z: %s, w: %s}" % rot, doc)
    new = re.sub(r"m_LocalPosition: \{[^}]*\}", "m_LocalPosition: {x: %s, y: %s, z: %s}" % pos, new)
    new = re.sub(r"m_LocalEulerAnglesHint: \{[^}]*\}", "m_LocalEulerAnglesHint: {x: 0, y: -90, z: 0}", new)
    return text.replace(doc, new, 1)


scene = g.read(TANDAVA_SCENE)
scene = lib.swap_guid(scene, T["controller"], G_SCRIPT["ShivaController"], "controller")
scene = lib.swap_guid(scene, T["settings"], G_ASSET["Settings"], "settings")
scene = lib.swap_guid(scene, T["rule"], G_ASSET["Rule"], "scoring rule")
scene = lib.swap_guid(scene, T["cell"], G_ASSET["CellConfig"], "cell config")
# the squad comes in a line abreast at +z, ~1,380 u out, facing -x: along the circuit, which runs anticlockwise from above
# (+theta), so the first ring is ~410 u ahead of them. The server picks a seed whose first ring opens there
# (ShivaDirectorCore.SeedFacing, from ShivaController.HandleTurnStarted) - the lab's squads start the same way (EntryPoint).
for k, fid in enumerate(SPAWN_FIDS):
    scene = move_transform(scene, fid, (0, 0, num(1290 + 60 * k)), ("0", "-0.70710677", "0", "0.70710677"))
g.emit_scene(SCENE_NAME, G_ASSET["Scene"], scene)

# ═════════════════════════════════════════════════════════════════════════════
# 5. The registries: a voyage is on the master roster and on no grid
# ═════════════════════════════════════════════════════════════════════════════
g.register_game_list(G_ASSET["Card"], lib.GAME_LIST)
g.register_always_unlocked()
g.register_build_scene("MinigameTandava", SCENE_NAME, G_ASSET["Scene"])

# ═════════════════════════════════════════════════════════════════════════════
# 6. Validation
# ═════════════════════════════════════════════════════════════════════════════
for roster in (lib.ARCADE_GRID, lib.ARENA_GRID):
    if G_ASSET["Card"] in g.read_current(roster):
        errors.append(f"the voyage card is on {roster} (a voyage is on no grid)")
for enum_file in (GAME_MODES_CS, TOASTS_CS):
    vals = re.findall(r"^\s+\w+\s*=\s*(\d+)\s*,", g.read(enum_file), re.M)
    dups = sorted({v for v in vals if vals.count(v) > 1}, key=int)
    if dups:
        errors.append(f"{enum_file} has duplicate values {dups}")
sc = g.files[f"{lib.SCENES_DIR}/{SCENE_NAME}.unity"]
if T["controller"] in sc:
    errors.append("the scene still names TandavaController")
for label, gg in (("ShivaController", G_SCRIPT["ShivaController"]), ("ShivaSettings", G_ASSET["Settings"]), ("ShivaScoringRule", G_ASSET["Rule"]),
                  ("Shiva Cell Config", G_ASSET["CellConfig"])):
    if gg not in sc:
        errors.append(f"the scene does not name {label}")
settings_text = g.files[SETTINGS_REL]
if "  - DisplayName" not in settings_text or settings_text.count("  - DisplayName") != 3:
    errors.append("ShivaSettings does not carry exactly three forms")
if re.search(r"Index: -1\b", settings_text):
    errors.append("a serpent plan index did not resolve")
# the lab and the director's C# defaults agree (the harness's S1 holds the layout itself to the lab's code)
def default_drift(src):
    out = []
    for name, value in DIRECTOR:
        mm = re.search(rf"\b{name} = (-?[0-9.]+)f?[,;]", src)
        if not mm:
            out.append(f"ShivaDirectorSettings.{name} has no default in ShivaDirectorCore.cs")
        elif abs(float(mm.group(1)) - float(value)) > 1e-6:
            out.append(f"ShivaDirectorSettings.{name} defaults to {mm.group(1)} but the lab ships {value}")
    return out


src = g.read(SCRIPT_PATHS["ShivaDirectorCore"])
errors += default_drift(src)
# negative control: the same check over a source whose Mouth default moved by one must report it
planted = re.sub(r"\bMouth = ([0-9.]+)f", lambda m: f"Mouth = {float(m.group(1)) + 1}f", src, count=1)
if planted == src or not any("Mouth" in e for e in default_drift(planted)):
    errors.append("negative control failed: a moved Mouth default would not be noticed")
for k, rel in SPECIES.items():
    if not os.path.exists(os.path.join(ROOT, rel)):
        errors.append(f"species {k} ({rel}) is not in the repo")

print(f"Shiva's Cell: mode {MODE_ID}; plans many-headed 5/7/10 at {[plans(h)['body'] for h in (5, 7, 10)]}; "
      f"{len(HULLS)} hulls; cell {num(CELL_RADIUS)} u; drums {LAB['clockP1']}/{LAB['clockP2']}/{LAB['clockP3']} s")
g.finish(errors, referenced=EXISTING, minted=list(G_SCRIPT.values()) + list(G_ASSET.values()) + [guid("folder/" + CELL_DIR)])
