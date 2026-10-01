#!/usr/bin/env python3
"""Author the SWARM FAUNA and the SWARM CELL - every asset, from one model.   (Docs/SWARM_FAUNA.md)

    python3 Tools/Build/author_swarm_fauna.py            # write
    python3 Tools/Build/author_swarm_fauna.py --check    # FAIL on any drift (reads the disk)

What it owns:
  * the four baked body plans (Tools/Build/swarm_plans.py, from the research branch), only when
    the research ref is reachable - otherwise the committed plans are checked for shape only;
  * the .meta of every new script (stable guids, one owner each);
  * SwarmTadpole.prefab - TadPoleFauna.prefab's body (its TadpoleSpindle + body prism instance,
    overrides intact) with the Boid, the per-tadpole FMOD loop, the NetworkObject /
    NetworkTransform / FaunaNetworkSync and the authored crystal REMOVED (a member provisions
    its heart from its element at birth and is client-local);
  * SwarmFauna.prefab - the heartless, bodiless population anchor;
  * SwarmFaunaConfig.asset - every number the swarm runs on;
  * the Swarm cell: config, spawn profile, three swarm species configs (the three populations)
    and three flora configs (their feeding grounds), and its entry in Menu_Main's
    Cell.CellConfigs (the Cell Selector reads that list; it authors none of its own).

THE CELL - three populations, separated in the cytoplasm by RADIUS (the platform's per-species
band, FaunaConfigurationSO.BandInner/BandOuterRadius), each over its own feeding ground:

    inner  430-600u   starts MASS   -> the whale       grazes MASS flora   (Arbor)
    mid    660-840u   starts TIME   -> the dragonfly   grazes SPACE flora  (Spire)
    outer  900-1120u  starts CHARGE -> the pufferfish  grazes TIME flora   (Frond)

The pairing is deliberate (Docs/SWARM_FAUNA.md §5): an egg is cheap when paid for with food of
its own element, and each ground feeds the creature that the resident becomes when a player kills
its majority - so a morph moves the swarm toward what its ground can sustain. CHARGE flora is
never a ground at all: a Charge plant's leaves are armoured and shielded mass is never food
(CLAUDE.md, the Charge law), so a pufferfish always pays the cross-element price for its shell.
"""
import argparse
import hashlib
import math
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
assert os.path.isdir(os.path.join(REPO, "Assets")), REPO
sys.path.insert(0, HERE)
import swarm_plans  # noqa: E402

A = lambda *p: os.path.join(REPO, "Assets", *p)
SWARM_DIR = A("_SO_Assets", "Swarm Fauna")
PLAN_DIR = os.path.join(SWARM_DIR, "Plans")
CELL_DIR = A("_SO_Assets", "Cell Configs", "Swarm Cell")
PREFAB_DIR = A("_Prefabs", "FloraAndFauna")
SCRIPT_DIR = A("_Scripts", "Controller", "Environment", "FloraAndFauna", "Swarm")
MENU_SCENE = A("_Scenes", "Menu_Main.unity")
TADPOLE_SRC = os.path.join(PREFAB_DIR, "TadPoleFauna.prefab")

SCRIPTS = ["SwarmFieldCore", "SwarmFaunaConfigSO", "SwarmPlanLibrary", "SwarmFauna", "SwarmTadpoleFauna"]
SO_SCRIPT = {
    "cell": "01f934d50526431a9392a6ceca1dc33d",
    "profile": "e8d8aa5d835249798a256e18f2f7d912",
    "flora": "a32a297a7606432885f4d3e1f83bea9a",
    "fauna": "c778cfbe4dfc4c5c8401e40c17802311",
}
RUNTIME_CELL_DATA = "8d4e8398eedc76c4dadb8604f89b9e1b"
MEMBRANE = ("346633111830028674", "6e330f85972faf843b8a128e7166f7b5")
NUCLEUS = ("7555898194514117247", "b9cf1833fa2493d4b8724ccb6740fb3a")
CYTOPLASM = ("639495419069806261", "9cacd903fcf4643459f5f14ac811bb20")
MODIFIER = ("8058406376250941529", "daa37ae0e7af4b04383c1c4e6e76817d")
ICON = "6aa1c06e11b265744a5f9fa8858ac72a"           # shared with the Arboretum - no new art
MEMBRANE_RADIUS = 1200.0
NUCLEUS_RADIUS = 392.0

# fileIDs this script mints (stable; unique inside their own files)
ROOT_MB_FID = "4174204561870355101"        # SwarmFauna on SwarmFauna.prefab
ROOT_GO_FID = "4174204561870355102"
ROOT_TR_FID = "4174204561870355103"
TADPOLE_MB_FID = "5945480239701989318"     # TadPoleFauna's Boid MB fileID, kept for the member

# ── the sim (research units; Docs/SWARM_FAUNA.md "Findings for the research") ─────────────
UNIT_SCALE = 2.0          # world units per voxel
TICK_HZ = 10.0
SEED_MEMBERS = 96         # OVERTUNE (x4): a hatchling swarm is already a half-built creature
SWARMS_PER_BAND = 8       # OVERTUNE (x8): each shell holds a school of swarms, not one
MAX_SPAWNS_PER_FRAME = 48 # cell-wide budget of tadpole Instantiates per frame (queued past it)
PRISM_SCALE = 1.0
HEARTS = {"Charge": 2.298, "Mass": 1.737, "Space": 2.298, "Time": 1.737}  # = Tadpole Fauna * (read below)

# ── the three populations ────────────────────────────────────────────────────────────────
ELEMENT_ID = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}
REGIONS = [
    # name,   band (world),   start,   plan,         ground species, ground element, canonical asset, plants floor/cap
    dict(key="Inner", band=(430, 600), start="Mass", plan="whale",
         flora="Arbor", food="Mass", canon="Arbor Flora Mass", floor=48, cap=100),
    dict(key="Middle", band=(660, 840), start="Time", plan="dragonfly",
         flora="Spire", food="Space", canon="Spire Flora Space", floor=64, cap=140),
    dict(key="Outer", band=(900, 1120), start="Charge", plan="pufferfish",
         flora="Frond", food="Time", canon="Frond Flora Time", floor=80, cap=160),
]
FLORA_GROWTH_PER_OFFSPRING = 0.8   # x the plant's own budget: a plant seeds a neighbour as it completes
FLORA_COOLDOWN = 20
FLORA_SPREAD = 120

# ladder ratios (against the modelled mature cell; see ladder())
RESTLESS_ENTER, RESTLESS_EXIT, FRENZY_ENTER, FRENZY_EXIT = 0.35, 0.26, 2.5, 2.2
LATTICE_HEART_COLLIDERS = 1080
# OVERTUNE PASS (user-authorized, "the next order of magnitude across the board"): this cell
# deliberately exceeds the Lattice cell's heart-collider budget. The gate is restated as an explicit
# overtune ceiling so it still FAILS if the numbers drift further, and the doc states the cost.
OVERTUNE_HEART_CEILING = 6000
ATLANTIS_PRISMS = 69000


def guid(name):
    g = hashlib.md5(("cosmicshore-swarm-fauna:" + name).encode()).hexdigest()
    return g


def rel(path):
    return os.path.relpath(path, REPO).replace(os.sep, "/")


def read(path):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        return fh.read()


def meta_guid(path):
    return re.search(r"^guid: ([0-9a-f]{32})", read(path + ".meta"), re.M).group(1)


FOLDER_META = ("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
SCRIPT_META = ("fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n"
               "  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")
TEXT_META = ("fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n"
             "  assetBundleName: \n  assetBundleVariant: \n")
PREFAB_META = ("fileFormatVersion: 2\nguid: %s\nPrefabImporter:\n  externalObjects: {}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")
ASSET_META = ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
              "  mainObjectFileID: 11400000\n  mainObjectFileType: 2\n  userData:\n  assetBundleName:\n"
              "  assetBundleVariant:\n")
SO_HEADER = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: %s
  m_EditorClassIdentifier:
"""

EMPTY_EVENT = ("    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n")


def script_guid(name):
    return guid(f"{rel(SCRIPT_DIR)}/{name}.cs")


# ── plans ───────────────────────────────────────────────────────────────────────────────────

def plans_from_research():
    """The baked plans, or None when the research ref is not reachable from this clone."""
    try:
        subprocess.run(["git", "-C", REPO, "rev-parse", "--verify", "-q", swarm_plans.RESEARCH_REF],
                       check=True, capture_output=True)
    except subprocess.CalledProcessError:
        return None
    return swarm_plans.bake_all(repo=REPO)


def plan_path(kind):
    return os.path.join(PLAN_DIR, f"SwarmPlan_{kind}.json")


def committed_plans():
    import json
    return {k: json.loads(read(plan_path(k))) for k in swarm_plans.KINDS}


def typical_half(plans):
    """Median half-extents of each research element across all four plans (SwarmPlanLibrary's rule)."""
    out = {}
    for e in range(4):
        xs, ys, zs = [], [], []
        for p in plans.values():
            for k in range(p["n"]):
                if p["elem"][k] == e:
                    xs.append(p["half"][3 * k]); ys.append(p["half"][3 * k + 1]); zs.append(p["half"][3 * k + 2])
        med = lambda v: sorted(v)[len(v) // 2]
        out[e] = (med(xs), med(ys), med(zs))
    return out


def egg_volumes(plans):
    """An egg costs the WORLD volume of a typical body prism of its element: a swarm converts eaten
    flora volume into its own body 1:1 (mass is conserved, nothing is minted)."""
    m = 2 * UNIT_SCALE * PRISM_SCALE
    return {e: round(h[0] * m * h[1] * m * h[2] * m, 2) for e, h in typical_half(plans).items()}


# ── prefabs ─────────────────────────────────────────────────────────────────────────────────

def split_docs(text):
    parts = re.split(r"(?m)^(?=--- !u!)", text)
    head, docs = parts[0], parts[1:]
    return head, docs


def doc_fid(doc):
    return re.match(r"--- !u!\d+ &(-?\d+)", doc).group(1)


def tadpole_prefab():
    """TadPoleFauna's body, as a swarm member. Derived from the shipped prefab every time, so a fix
    to the tadpole's spindle or body prism flows through."""
    head, docs = split_docs(read(TADPOLE_SRC))
    by = {doc_fid(d): d for d in docs}
    crystal_instance = "4724174359583210385"
    crystal_owned = {fid for fid, d in by.items()
                     if f"m_PrefabInstance: {{fileID: {crystal_instance}}}" in d
                     or re.search(r"m_GameObject: \{fileID: (1802737087398988225|1936952849576087959|"
                                  r"3509960624814372404|7185464839235080837|8204112650180281118)\}", d)}
    drop = {crystal_instance, "6009907184062220078", "1065634054817553135", "262028545687005572",
            "509837155879624528"} | crystal_owned
    out = []
    for d in docs:
        fid = doc_fid(d)
        if fid in drop:
            continue
        if fid == "5119337659833296837":          # root GameObject
            d = re.sub(r"  m_Component:\n(  - component: \{fileID: -?\d+\}\n)+",
                       f"  m_Component:\n  - component: {{fileID: 5494491424606190975}}\n"
                       f"  - component: {{fileID: {TADPOLE_MB_FID}}}\n", d)
            d = d.replace("m_Name: MassTadPoleFauna", "m_Name: SwarmTadpole")
        elif fid == "5494491424606190975":        # root Transform
            d = re.sub(r"m_LocalRotation: \{[^}]*\}", "m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}", d)
            d = re.sub(r"m_LocalScale: \{[^}]*\}", "m_LocalScale: {x: 1, y: 1, z: 1}", d)
            d = re.sub(r"  m_Children:\n(  - \{fileID: -?\d+\}\n)+",
                       "  m_Children:\n  - {fileID: 147158808990258422}\n", d)
            d = re.sub(r"m_LocalEulerAnglesHint: \{[^}]*\}", "m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}", d)
        elif fid == "5945480239701989318":        # the Boid -> the swarm member
            d = d[:d.index("  m_Script:")] + (
                f"  m_Script: {{fileID: 11500000, guid: {script_guid('SwarmTadpoleFauna')}, type: 3}}\n"
                "  m_Name: \n  m_EditorClassIdentifier: \n"
                f"  cellData: {{fileID: 11400000, guid: {RUNTIME_CELL_DATA}, type: 2}}\n"
                "  domain: 1\n  goalUpdateInterval: 5\n  goalUpdateIntervalByAggression:\n  - 1\n  - 0.55\n  - 0.25\n"
                "  goalOrbitRadius: 0\n  diet: 0\n  predationImmunitySeconds: 2\n  starvationSeconds: 0\n"
                "  bodyPrism: {fileID: 6449687985229987934}\n")
        elif fid == "4754995950771878307":        # the spindle instance: drop its per-tadpole loop
            d = d.replace("    m_RemovedComponents: []\n",
                          "    m_RemovedComponents:\n    - {fileID: -6330896111675949634, guid: 32182c0d19e344b4c9a9b332052037e6,\n"
                          "        type: 3}\n")
        out.append(d)
    text = head + "".join(out)
    text = text.replace(f"--- !u!114 &{TADPOLE_MB_FID}\n", f"--- !u!114 &{TADPOLE_MB_FID}\n", 1)
    return text


def anchor_prefab():
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{ROOT_GO_FID}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {ROOT_TR_FID}}}
  - component: {{fileID: {ROOT_MB_FID}}}
  m_Layer: 0
  m_Name: SwarmFauna
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{ROOT_TR_FID}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {ROOT_GO_FID}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{ROOT_MB_FID}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {ROOT_GO_FID}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid('SwarmFauna')}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  cellData: {{fileID: 11400000, guid: {RUNTIME_CELL_DATA}, type: 2}}
  domain: 1
  goalUpdateInterval: 3
  goalUpdateIntervalByAggression:
  - 1
  - 1
  - 1
  goalOrbitRadius: 0
  diet: 0
  predationImmunitySeconds: 0
  starvationSeconds: 0
  config: {{fileID: 11400000, guid: {guid(rel(os.path.join(SWARM_DIR, 'SwarmFaunaConfig.asset')))}, type: 2}}
"""


# ── the swarm config ────────────────────────────────────────────────────────────────────────

def v4(d):
    return "{x: %s, y: %s, z: %s, w: %s}" % tuple(_g(d[k]) for k in ("Charge", "Mass", "Space", "Time"))


def _g(x):
    return ("%.3f" % x).rstrip("0").rstrip(".")


def config_asset(eggs):
    egg = {"Charge": eggs[0], "Mass": eggs[1], "Space": eggs[2], "Time": eggs[3]}
    pg = lambda k: guid(rel(plan_path(k)))
    return SO_HEADER % (script_guid("SwarmFaunaConfigSO"), "SwarmFaunaConfig") + (
        f"  ChargePlan: {{fileID: 4900000, guid: {pg('charge')}, type: 3}}\n"
        f"  MassPlan: {{fileID: 4900000, guid: {pg('mass')}, type: 3}}\n"
        f"  SpacePlan: {{fileID: 4900000, guid: {pg('space')}, type: 3}}\n"
        f"  TimePlan: {{fileID: 4900000, guid: {pg('time')}, type: 3}}\n"
        f"  TadpolePrefab: {{fileID: {TADPOLE_MB_FID}, guid: {guid(rel(os.path.join(PREFAB_DIR, 'SwarmTadpole.prefab')))}, type: 3}}\n"
        f"  UnitScale: {_g(UNIT_SCALE)}\n  TickHz: {_g(TICK_HZ)}\n  MaxStepsPerFrame: 3\n"
        f"  SeedMembers: {SEED_MEMBERS}\n"
        "  BiteRadius: 10\n  BitersPerStep: 96\n"
        f"  EggVolume: {v4(egg)}\n"
        f"  CrossElementCost: 2\n  StomachEggs: 240\n  LayRate: 0.2\n  LayMax: 16\n  MaxSpawnsPerFrame: {MAX_SPAWNS_PER_FRAME}\n  KillLayHoldSeconds: 2\n"
        "  StarvationSeconds: 90\n  ShedIntervalSeconds: 1\n  ExtinctLingerSeconds: 10\n"
        "  Cruise: 0.35\n  TurnPerStep: 0.03\n  WanderReach: 300\n"
        "  VesselRadius: 9\n  SenseMargin: 400\n  DangerEnter: 0.45\n  DangerExit: 0.15\n"
        f"  HeartWorldScale: {v4(HEARTS)}\n"
        f"  PrismScale: {_g(PRISM_SCALE)}\n  HeartPrismGap: 0.6\n  BirthBloomSeconds: 0.8\n  MoltHeartSeconds: 0.5\n"
        "  SwarmLoopEvent:\n" + EMPTY_EVENT.replace("    ", "    ", 1) +
        "  MorphEvent:\n" + EMPTY_EVENT)


# ── the cell ────────────────────────────────────────────────────────────────────────────────

PREFIX = "Swarm"


def cell_path(name):
    return os.path.join(CELL_DIR, name + ".asset")


def fauna_name(r):
    return f"{PREFIX} {r['key']} {r['start']} Swarm Fauna Config Data"


def flora_name(r):
    return f"{PREFIX} {r['key']} {r['flora']} Flora {r['food']} Config Data"


def canon(r):
    path = A("_SO_Assets", "Lifeforms", r["canon"] + ".asset")
    text = read(path)
    leaf = re.search(r"LeafSize: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+)\}", text).groups()
    budget = int(re.search(r"MaxTotalSpawnedObjects: (\d+)", text).group(1))
    prefab = re.search(r"FloraPrefab: \{fileID: (-?\d+), guid: (\w+)", text).groups()
    seg = float(re.search(r"segmentLength: ([\d.]+)", read(
        [p for p in (A("_Prefabs", "FloraAndFauna", r["flora"] + "Flora.prefab"),) if os.path.exists(p)][0])).group(1))
    return dict(guid=meta_guid(path), leaf=tuple(map(float, leaf)), budget=budget, prefab=prefab, seg=seg)


def fauna_asset(r):
    lo, hi = r["band"]
    return SO_HEADER % (SO_SCRIPT["fauna"], fauna_name(r)) + (
        f"  FaunaPrefab: {{fileID: {ROOT_MB_FID}, guid: {guid(rel(os.path.join(PREFAB_DIR, 'SwarmFauna.prefab')))}, type: 3}}\n"
        f"  InitialSpawnCount: {SWARMS_PER_BAND}\n  PopulationSize: {SWARMS_PER_BAND}\n  SpawnProbability: 1\n  NetworkSynced: 0\n"
        "  FeedsPerOffspring: 0\n  OffspringPerBirth: 1\n  ReproductionCooldownSeconds: 10\n"
        f"  MaxLivePopulation: {SWARMS_PER_BAND}\n  ReleaseTier: 0\n"
        f"  BandInnerRadius: {lo}\n  BandOuterRadius: {hi}\n  CenterFocusBias: 0\n"
        f"  Element: {ELEMENT_ID[r['start']]}\n"
        "  Variant:\n    Enabled: 0\n"
        "  SpreadElements: 0\n  ElementPalette: []\n")


def flora_asset(r, c):
    lo, hi = r["band"]
    quota = max(1, int(round(c["budget"] * FLORA_GROWTH_PER_OFFSPRING)))
    return SO_HEADER % (SO_SCRIPT["flora"], flora_name(r)) + (
        f"  FloraPrefab: {{fileID: {c['prefab'][0]}, guid: {c['prefab'][1]}, type: 3}}\n"
        "  NetworkSynced: 0\n  SpawnProbability: 1\n"
        f"  InitialSpawnCount: {r['floor']}\n  OverrideDefaultPlantPeriod: 0\n  NewPlantPeriod: 2147483647\n"
        f"  PopulationSize: {r['floor']}\n  MaxLivePopulation: {r['cap']}\n"
        f"  GrowthPerOffspring: {quota}\n  OffspringPerBirth: 1\n"
        f"  ReproductionCooldownSeconds: {FLORA_COOLDOWN}\n  MaturityFraction: 0.5\n"
        f"  OffspringSpread: {FLORA_SPREAD}\n  PreferredSites: 0\n"
        f"  Element: {ELEMENT_ID[r['food']]}\n  Variant:\n    Enabled: 0\n"
        "  SpreadElements: 1\n"
        f"  ElementPalette:\n  - {{fileID: 11400000, guid: {c['guid']}, type: 2}}\n"
        f"  PlantRadiusCellFractionMaxOverride: {_g(hi / MEMBRANE_RADIUS)}\n"
        f"  PlantRadiusCellFractionMinOverride: {_g(lo / MEMBRANE_RADIUS)}\n"
        "  MaxTotalSpawnedObjectsOverride: -1\n")


def model(plans):
    """The mature cell, modelled (Docs/SWARM_FAUNA.md §6 says how to re-measure it in the editor).

    A phyllotactic prism is a strut: its cross-section is the leaf's x*y and its length is a
    segment or a whorl reach (PhyllotacticFlora), modelled at 0.55 x segmentLength. Swarms are
    counted at their LARGEST plan's headcount and their typical prism volume."""
    eggs = egg_volumes(plans)
    rows, tot = [], dict(prisms=0, volume=0.0, hearts=0, plants_max=0, tadpoles=0)
    for r in REGIONS:
        c = canon(r)
        per = c["leaf"][0] * c["leaf"][1] * 0.55 * c["seg"]
        plants = r["cap"]
        rows.append(dict(r=r, c=c, per=per, plant_volume=per * c["budget"]))
        tot["prisms"] += plants * c["budget"]
        tot["volume"] += plants * c["budget"] * per
        tot["plants_max"] += plants
    cap = max(p["n"] for p in plans.values())
    tot["tadpoles"] = cap * len(REGIONS) * SWARMS_PER_BAND
    tot["prisms"] += tot["tadpoles"]
    tot["volume"] += tot["tadpoles"] * max(eggs.values())
    tot["hearts"] = tot["plants_max"] + tot["tadpoles"]
    return rows, tot, eggs


def ladder(tot):
    rt = lambda x, s: int(math.ceil(x / s) * s)
    return {
        "RestlessEnter": rt(tot["prisms"] * RESTLESS_ENTER, 100),
        "RestlessExit": rt(tot["prisms"] * RESTLESS_EXIT, 100),
        "FrenzyEnter": rt(tot["prisms"] * FRENZY_ENTER, 100),
        "FrenzyExit": rt(tot["prisms"] * FRENZY_EXIT, 100),
        "RestlessEnterVolume": rt(tot["volume"] * RESTLESS_ENTER, 1000),
        "RestlessExitVolume": rt(tot["volume"] * RESTLESS_EXIT, 1000),
        "FrenzyEnterVolume": rt(tot["volume"] * FRENZY_ENTER, 1000),
        "FrenzyExitVolume": rt(tot["volume"] * FRENZY_EXIT, 1000),
    }


def profile_asset():
    floras = "".join(f"  - {{fileID: 11400000, guid: {guid(rel(cell_path(flora_name(r))))}, type: 2}}\n" for r in REGIONS)
    faunas = "".join(f"  - {{fileID: 11400000, guid: {guid(rel(cell_path(fauna_name(r))))}, type: 2}}\n" for r in REGIONS)
    return SO_HEADER % (SO_SCRIPT["profile"], f"{PREFIX} Cell Spawn Profile") + (
        "  FloraExcludeLocalDomain: 0\n  FloraSpawnVolumeCeiling: 12000\n  FloraInitialDelaySeconds: 0\n"
        "  FloraSpawnIntervalSeconds: 0\n  FloraPopulationScale: 1\n  FloraPlantBudgetScale: 1\n"
        "  SupportedFloras:\n" + floras +
        "  FaunaExcludeLocalDomain: 0\n  InitialFaunaSpawnWaitTime: 6\n  FaunaSpawnVolumeThreshold: 1\n"
        "  FaunaPopulationScale: 1\n  BaseFaunaSpawnTime: 30\n  SeedFullWaveEveryTick: 0\n"
        "  FaunaFoodFloor: 0\n  FaunaInitialDelaySeconds: 0\n  FaunaSpawnIntervalSeconds: 0\n"
        "  HerbivoreSpawnPointCount: 3\n  HerbivoreSpawnRadius: 800\n  PredatorSpawnPointCount: 0\n"
        "  PredatorSpawnRadius: 900\n"
        "  SupportedFaunas:\n" + faunas)


def cell_asset(L):
    return SO_HEADER % (SO_SCRIPT["cell"], f"{PREFIX} Cell Config") + (
        "  CellName: Swarm\n"
        "  Description: Three tadpole swarms in three shells of the cytoplasm - a whale, a dragonfly\n"
        "    and a pufferfish - each over its own feeding ground. Kill a body's majority element\n"
        "    and it becomes another animal\n"
        f"  Icon: {{fileID: 21300000, guid: {ICON}, type: 3}}\n"
        "  Difficulty: 2\n  CellEndGameScore: 0\n"
        f"  MembranePrefab: {{fileID: {MEMBRANE[0]}, guid: {MEMBRANE[1]}, type: 3}}\n"
        f"  NucleusPrefab: {{fileID: {NUCLEUS[0]}, guid: {NUCLEUS[1]}, type: 3}}\n"
        f"  CytoplasmPrefab: {{fileID: {CYTOPLASM[0]}, guid: {CYTOPLASM[1]}, type: 3}}\n"
        "  CellModifiers:\n"
        f"  - {{fileID: {MODIFIER[0]}, guid: {MODIFIER[1]}, type: 3}}\n"
        f"  SpawnProfile: {{fileID: 11400000, guid: {guid(rel(cell_path(PREFIX + ' Cell Spawn Profile')))}, type: 2}}\n"
        "  PhaseThresholds:\n" + "".join(f"    {k}: {v}\n" for k, v in L.items()))


# ── Menu_Main ───────────────────────────────────────────────────────────────────────────────

def scene_patch(text, g):
    m = re.search(r"( *- target: \{fileID: (\d+), guid: ([a-f0-9]{32}),\n"
                  r" *type: 3\}\n *propertyPath: CellConfigs\.Array\.size\n"
                  r" *value: )(\d+)(\n)", text)
    if not m:
        raise SystemExit("Menu_Main: no CellConfigs.Array.size override to extend")
    size = int(m.group(4))
    tf, tg = m.group(2), m.group(3)
    text = text[:m.start(4)] + str(size + 1) + text[m.end(4):]
    last = None
    for hit in re.finditer(r" *- target: \{fileID: \d+, guid: [a-f0-9]{32},\n"
                           r" *type: 3\}\n *propertyPath: 'CellConfigs\.Array\.data\[\d+\]'\n"
                           r" *value: \n *objectReference: \{fileID: 11400000, guid: [a-f0-9]{32},\n"
                           r" *type: 2\}\n", text):
        last = hit
    if last is None:
        raise SystemExit("Menu_Main: no CellConfigs entry to append after")
    entry = (f"    - target: {{fileID: {tf}, guid: {tg},\n        type: 3}}\n"
             f"      propertyPath: 'CellConfigs.Array.data[{size}]'\n      value: \n"
             f"      objectReference: {{fileID: 11400000, guid: {g},\n        type: 2}}\n")
    return text[:last.end()] + entry + text[last.end():], size


# ── assemble ────────────────────────────────────────────────────────────────────────────────

def emit():
    """path -> text, for every file this script owns."""
    out = {}
    baked = plans_from_research()
    plans = baked if baked else committed_plans()
    if baked:
        for k, p in baked.items():
            out[plan_path(k)] = swarm_plans.dumps(p)
    for k in swarm_plans.KINDS:
        out[plan_path(k) + ".meta"] = TEXT_META % guid(rel(plan_path(k)))
    for d in (SWARM_DIR, PLAN_DIR, CELL_DIR, SCRIPT_DIR):
        out[d + ".meta"] = FOLDER_META % guid(rel(d))
    for s in SCRIPTS:
        out[os.path.join(SCRIPT_DIR, s + ".cs.meta")] = SCRIPT_META % script_guid(s)

    rows, tot, eggs = model(plans)
    for name, text in (("SwarmTadpole.prefab", tadpole_prefab()), ("SwarmFauna.prefab", anchor_prefab())):
        p = os.path.join(PREFAB_DIR, name)
        out[p] = text
        out[p + ".meta"] = PREFAB_META % guid(rel(p))
    cfgp = os.path.join(SWARM_DIR, "SwarmFaunaConfig.asset")
    out[cfgp] = config_asset(eggs)
    out[cfgp + ".meta"] = ASSET_META % guid(rel(cfgp))

    L = ladder(tot)
    for r, row in zip(REGIONS, rows):
        for name, text in ((fauna_name(r), fauna_asset(r)), (flora_name(r), flora_asset(r, row["c"]))):
            out[cell_path(name)] = text
            out[cell_path(name) + ".meta"] = ASSET_META % guid(rel(cell_path(name)))
    for name, text in ((PREFIX + " Cell Spawn Profile", profile_asset()), (PREFIX + " Cell Config", cell_asset(L))):
        out[cell_path(name)] = text
        out[cell_path(name) + ".meta"] = ASSET_META % guid(rel(cell_path(name)))
    return out, rows, tot, eggs, L, bool(baked)


def verify(out, tot, rows):
    problems = []
    # every guid this script mints has exactly one owner in the tree (or will, once written)
    minted = {re.search(r"^guid: (\w+)", t, re.M).group(1): p for p, t in out.items() if p.endswith(".meta")}
    for g, p in minted.items():
        owners = subprocess.run(["grep", "-rl", "--include=*.meta", f"^guid: {g}", A()],
                                capture_output=True, text=True).stdout.splitlines()
        owners = [o for o in owners if os.path.abspath(o) != os.path.abspath(p)]
        if owners:
            problems.append(f"guid {g} of {rel(p)} is already owned by {rel(owners[0])}")
    # the populations are separated: bands disjoint, outside the nucleus, inside the membrane
    for a, b in zip(REGIONS, REGIONS[1:]):
        if a["band"][1] >= b["band"][0]:
            problems.append(f"bands overlap: {a['key']} {a['band']} vs {b['key']} {b['band']}")
    if REGIONS[0]["band"][0] <= NUCLEUS_RADIUS:
        problems.append("the inner band reaches into the nucleus")
    if REGIONS[-1]["band"][1] >= MEMBRANE_RADIUS:
        problems.append("the outer band reaches the membrane")
    if len({r["start"] for r in REGIONS}) != len(REGIONS):
        problems.append("two populations start as the same creature")
    if any(r["food"] == "Charge" for r in REGIONS):
        problems.append("a Charge feeding ground is no food at all (armoured leaves)")
    if tot["hearts"] >= OVERTUNE_HEART_CEILING:
        problems.append(f"{tot['hearts']} always-on heart colliders >= the overtune ceiling {OVERTUNE_HEART_CEILING}")
    # the prefab really is stripped of its network layer and its authored crystal
    tp = out[os.path.join(PREFAB_DIR, "SwarmTadpole.prefab")]
    for bad, what in (("d5a57f767e5e46a458fc5d3c628d0cbb", "NetworkObject"), ("818b214228314119900f4d9860f0762d", "FaunaNetworkSync"),
                      ("cccc6ba7985893f43841fccfbb53dc71", "authored crystal"), ("19b38987ca9e2974083abe50717ed186", "Boid")):
        if bad in tp:
            problems.append(f"SwarmTadpole.prefab still carries the {what}")
    fids = re.findall(r"(?m)^--- !u!\d+ &(-?\d+)", tp)
    if len(fids) != len(set(fids)):
        problems.append("SwarmTadpole.prefab has a duplicate fileID")
    for ref in set(re.findall(r"\{fileID: (-?\d+)\}", tp)) - {"0"}:
        if ref not in fids:
            problems.append(f"SwarmTadpole.prefab references a local fileID {ref} it no longer contains")
    return problems


def report(rows, tot, eggs, L, baked):
    print("Swarm cell - three populations in three shells of the cytoplasm\n")
    for row in rows:
        r, c = row["r"], row["c"]
        print(f"  {r['key']:<6} {r['band'][0]:>4}-{r['band'][1]:<4}u  starts {r['start']:<6} ({r['plan']:<10})  "
              f"grazes {r['food']:<5} {r['flora']:<5} x{r['floor']}..{r['cap']}  "
              f"{c['budget']} prisms/plant, ~{row['plant_volume']:,.0f} volume/plant (model)")
    print(f"\n  egg = one body prism's world volume: " + ", ".join(
        f"{n} {eggs[i]:.1f}" for i, n in enumerate(("Charge", "Mass", "Space", "Time"))))
    print(f"  colliders: {tot['hearts']} always-on hearts at the caps ({tot['tadpoles']} tadpoles + "
          f"{tot['plants_max']} plants) - the Lattice cell's is {LATTICE_HEART_COLLIDERS}; "
          f"+{tot['tadpoles']} tadpole body prisms")
    print(f"  mature cell (model): {tot['prisms']:,} prisms, {tot['volume']:,.0f} volume - Atlantis is {ATLANTIS_PRISMS:,}")
    print("  ladder: " + ", ".join(f"{k} {v:,}" for k, v in L.items()))
    print(f"  plans: {'re-baked from ' + swarm_plans.RESEARCH_REF if baked else 'research ref not reachable - committed plans kept'}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    out, rows, tot, eggs, L, baked = emit()
    report(rows, tot, eggs, L, baked)
    problems = verify(out, tot, rows)
    text = read(MENU_SCENE)
    listed = guid(rel(cell_path(PREFIX + " Cell Config"))) in text

    if args.check:
        drift = [rel(p) for p, t in out.items() if not os.path.exists(p) or read(p) != t]
        if not listed:
            drift.append(rel(MENU_SCENE) + " (Cell.CellConfigs does not list the Swarm cell)")
        if problems or drift:
            print("\nFAIL")
            for p in problems + [f"differs from what this script authors: {d}" for d in drift]:
                print("  - " + p)
            return 1
        print("\nOK - every asset matches the model and the Swarm cell is in the Cell Selector.")
        return 0

    if problems:
        print("\nFAIL - refusing to write:")
        for p in problems:
            print("  - " + p)
        return 1
    for p, t in out.items():
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(t)
    print(f"\nwrote {len(out)} files")
    if listed:
        print("  Cell Selector: already listed in Menu_Main")
    else:
        patched, size = scene_patch(text, guid(rel(cell_path(PREFIX + " Cell Config"))))
        with open(MENU_SCENE, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(patched)
        print(f"  Cell Selector: appended to Menu_Main's Cell.CellConfigs (size {size} -> {size + 1})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
