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
  * SwarmFauna.prefab / SwarmGridFauna.prefab / SwarmSortFauna.prefab / SwarmEvoFateFauna.prefab - the
    heartless, bodiless population anchors, one per simulation model (field / grid / sort / evofate);
  * SwarmFaunaConfig.asset / SwarmGridFaunaConfig.asset / SwarmSortFaunaConfig.asset /
    SwarmEvoFateFaunaConfig.asset - every number the swarm runs on, identical except Model;
  * SwarmEvoFateRule.json - the trained G2 network the evofate model runs (the research's
    results/live/evo_rule.json, re-packed for SwarmEvoRule.Parse);
  * the Swarm cell: config, spawn profile, four swarm species configs (the four populations)
    and four flora configs (their feeding grounds), and its entry in Menu_Main's
    Cell.CellConfigs (the Cell Selector reads that list; it authors none of its own).

THE CELL - four populations, separated in the cytoplasm by RADIUS (the platform's per-species
band, FaunaConfigurationSO.BandInner/BandOuterRadius), each over its own feeding ground:

    inner  430-560u   GRID     6 swarms  starts MASS   -> the whale       grazes MASS flora   (Arbor)
    mid    610-740u   FIELD    8 swarms  starts TIME   -> the dragonfly   grazes SPACE flora  (Spire)
    outer  790-920u   SORT     7 swarms  starts CHARGE -> the pufferfish  grazes TIME flora   (Frond)
    rim    970-1120u  EVOFATE  3 swarms  starts SPACE  -> the jellyfish   grazes SPACE flora  (Reed)

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
import author_builders  # noqa: E402  round 11e: the builder colonies live in this cell (their own generator)

A = lambda *p: os.path.join(REPO, "Assets", *p)
SWARM_DIR = A("_SO_Assets", "Swarm Fauna")
PLAN_DIR = os.path.join(SWARM_DIR, "Plans")
CELL_DIR = A("_SO_Assets", "Cell Configs", "Swarm Cell")
PREFAB_DIR = A("_Prefabs", "FloraAndFauna")
SCRIPT_DIR = A("_Scripts", "Controller", "Environment", "FloraAndFauna", "Swarm")
MENU_SCENE = A("_Scenes", "Menu_Main.unity")
TADPOLE_SRC = os.path.join(PREFAB_DIR, "TadPoleFauna.prefab")

SCRIPTS = ["ISwarmCore", "SwarmFieldCore", "SwarmGridCore", "SwarmSortCore", "SwarmEvoFateCore", "SwarmFaunaConfigSO", "SwarmPlanLibrary", "SwarmFauna",
           "SwarmTadpoleFauna", "SwarmTickJob", "SwarmMemberRenderer"]
# round 7: the GPU member shader (Docs/SWARM_FAUNA.md §14) and the palette its members wear
GRAPHS_DIR = A("_Graphics", "Materials", "Graphs")
MEMBER_SHADER = os.path.join(GRAPHS_DIR, "SwarmMemberInstanced.shader")
MEMBER_HLSL = os.path.join(GRAPHS_DIR, "SwarmMemberInstanced.hlsl")
THEME = "d45a23e6bd2da304988606fba6c97628"   # Assets/_SO_Assets/ThemeManagerDataContainer.asset
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
SEED_MEMBERS = 48         # per plan DENSITY unit (x PLAN_DENSITY in code): 240 tadpoles, a sparse ghost of the creature
TOTAL_SWARMS = 3          # ROUND 7: three big sort swarms (Docs/SWARM_FAUNA.md §14) - was 24 small ones on four models
MAX_SPAWNS_PER_FRAME = 24 # cell-wide budget of PROXY Instantiates per frame (round 7: births are free, proxies are not)
PLAN_DENSITY = 5          # ROUND 7: tadpoles per plan unit - the whale is 960, the pufferfish 895, the jellyfish 440
ENGAGE_RADIUS = 160       # world units around a vessel inside which a member is a real GameObject (a proxy)
MAX_PROXIES = 160         # per swarm: the nearest members win
BITERS_PER_STEP = 24      # bites per tick per swarm - the one main-thread cost that scales with appetite
PRISM_SCALE = 1.0
MULTI_DOMAIN = 1          # ROUND 9 (Docs/SWARM_FAUNA.md §17): the Swarm cell's swarms grow regional LINEAGES (food colours nothing)
LINEAGE_DRIFT = 0.01      # chance a child laid into unowned body tissue founds a new lineage (harness R9b at density 5)
FORAGE = (0.5, 0.9, 10, 60)   # ROUND 9 §17.1: ForageBelow, SatedAbove, GiveUpSeconds, PlantRestSeconds
HEARTS = {"Charge": 2.298, "Mass": 1.737, "Space": 2.298, "Time": 1.737}  # = Tadpole Fauna * (read below)

# ── the three populations ────────────────────────────────────────────────────────────────
ELEMENT_ID = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}
# ROUND 7 (Docs/SWARM_FAUNA.md §14): THREE swarms, all on the SORT model (research sortfeel + the 1-in-8
# update - the only one that passes the research hold, smoothness 0.917), each ~5x the round-6 body, each
# starting as a different creature so the cell still shows variety. The field, grid and evofate cores stay
# in the tree (Spawn Matrix, their configs below) - just not in this cell.
#
# FLORA: the BORROMEAN membrane, forked per band from the canonical species configs (the Wrecking Ball
# precedent: the cell's own generator owns the cell's copies). It is the cheapest family at steady state -
# no per-frame Update (PhyllotacticFlora's was 11% of the frame in the round-6b capture), and a COMPACT plant
# that closes and stops: a finished plant's grow tick is one compare. Few plants, sized against the egg bill
# of the three bodies (report() prints both). Charge is never food (armoured leaves), so the pufferfish band
# grows Time plants and its Charge majority is paid at the cross-element price - the feeding-ground lever.
# flora_band (round 11-10, Docs/SWARM_FAUNA.md §27): where a region's flora is PLANTED, when it is not the swarm's band.
# The Middle forest reaches into the 625-690 gap, the mobbers' shell: the bestiary's mobbers roost ON a plant
# (mobber.py: roost = the arena's plants), and penned to a plant-free gap the cell's 40 starved by minute 3 with 0 bites
# in 4,524 asks. Same plant count (no collider moves); about a quarter of the Middle plants stand in the gap, where the
# Middle swarm (penned to 690-840) does not graze. The Middle forest is also 9-12 plants (was 6-10, +2 always-on hearts,
# 1,194 of the 1,200 colliders): three sector pens (stampede, leech, leviathan) each cover a fifth of the shell, and at 6
# plants one pen in six held none even with SpreadPlanting. pens (FloraConfigurationSO.PlantingPens): each new Middle plant
# roots in whichever of those four pens holds the fewest - spread over the whole 625-840 shell, a sector or the gap still
# came up empty in one seed in four (12-seed sweep), and the population penned there starved.
# FLORA_INSET (round 11-14, Docs/SWARM_FAUNA.md §26.6): a plant roots this far INSIDE the pen it feeds (radially, and
# FLORA_INSET_DEG inside a sector's half-angle). A grazer seats at its plant (seed spread 30-40 u) and its food points are
# the plant's leaves (6-30 u about the heart): planted out to the band's edge, a seated lurker or mobber stood half outside
# its pen - 51% / 70% of member-seconds inside it (showcase C8) - and the pen pulled it back only once it had left.
FLORA_INSET, FLORA_INSET_DEG = 25, 10
REGIONS = [
    dict(key="Inner", band=(470, 620), start="Mass", plan="whale", model="Sort", swarms=1,
         flora="Borromean", food="Mass", floor=2, cap=3, flora_band=(470 + FLORA_INSET, 620 - FLORA_INSET)),
    dict(key="Middle", band=(690, 840), start="Charge", plan="pufferfish", model="Sort", swarms=1,
         flora="Borromean", food="Time", floor=9, cap=12, flora_band=(625 + FLORA_INSET // 2, 840 - FLORA_INSET),
         # (axis about the cell centre or None, half-angle deg, inner u, outer u): the three grazer sector pens of
         # author_substrate_fauna (stampede +X, leech +120, leviathan -120) and the mobbers' roost gap - each inset by
         # FLORA_INSET inside the population's pen (the gap is 60 u deep, so half that)
         pens=[((1, 0, 0), 55 - FLORA_INSET_DEG, 690 + FLORA_INSET, 840 - FLORA_INSET),
               ((-0.5, 0, 0.866), 55 - FLORA_INSET_DEG, 690 + FLORA_INSET, 840 - FLORA_INSET),
               ((-0.5, 0, -0.866), 55 - FLORA_INSET_DEG, 690 + FLORA_INSET, 840 - FLORA_INSET),
               (None, 0, 625 + FLORA_INSET // 2, 685 - FLORA_INSET // 2)]),
    dict(key="Outer", band=(910, 1080), start="Space", plan="jellyfish", model="Sort", swarms=1,
         flora="Borromean", food="Space", floor=3, cap=5, flora_band=(910 + FLORA_INSET, 1080 - FLORA_INSET)),
]
MODEL_ID = {"Field": 0, "Grid": 1, "Sort": 2, "EvoFate": 3}
# the grid model's game settings (SwarmGridCore; research combo = hgrid2 made lossless, unless noted).
# Ordered as SwarmFaunaConfigSO declares them. G8 (cell 12) is the shipped default: measured by
# Tools/Build/swarm_core_harness/score_combo.py it scores the same as G16 (game 12/13 at every seed,
# research 16/16 at seed 7) at roughly half the ms/step - Docs/SWARM_FAUNA.md §10.
GRID = dict(GridSize=8, GridCell=12, GridKClass=10, GridKTotal=1, GridPersist=0.6, GridNoise=0.05,
            GridLayChance=0.1, GridCrossChance=0.25, GridLayMaxPerStep=3, GridKFine=2, GridSigmaRel=1.2,
            GridSigma=3.5, GridMigrate=1, GridFeedForward=1.5)
GRID_TAIL = dict(GridPlanLock=30, GridLossless=1, GridMoltRate=0.04, GridMoltSteps=10, GridLayCap=1)
# the evofate model's game settings (SwarmEvoFateCore; research evofate C2). Its code and composition are
# sort's (the SORT fields); these are its own.
EVO = dict(EvoPull=2, EvoDeadZone=1.5, EvoAdhesion=0.35, EvoTimeSpeed=0.7, EvoTimeDeadZone=0.3)
GRID_PERIOD = {"Charge": 8, "Mass": 8, "Space": 8, "Time": 16}
# the sort model's game settings (SwarmSortCore; research sort's results/sort/params.json, rounded,
# unless noted). Written verbatim: these strings are the SO's values AND its C# defaults.
SORT = [("SortWellsPerType", "12"), ("SortUnitsPerWell", "4"), ("SortWellWidth", "0.89"), ("SortWellGain", "0.412"),
        ("SortWellClip", "0.525"), ("SortSpacing", "2.25"), ("SortRepulsion", "0.151"), ("SortAdhesionRadius", "5.38"),
        ("SortAdhesion", "{x: -0.05, y: -0.0374, z: -0.027, w: -0.0637}"), ("SortSwap", "0.709"),
        ("SortSwapRadius", "3.63"), ("SortInertia", "0.687"),
        ("SortNoise", "0.1"),          # GAME (research 0); round 6 kept it beside the wander: smoother (SWARM_FAUNA §12)
        ("SortFeedForward", "1"),      # GAME (research 0): members take their well's animation
        ("SortDwell", "12"), ("SortLayRate", "0.084"), ("SortLayMax", "5"), ("SortCrossChance", "0.466"),
        ("SortFillTolerance", "0.15"), ("SortBodyFill", "0.939"), ("SortMoltRate", "0.03"),
        ("SortMoltSteps", "10"),       # GAME (research instant): a molt is an animation
        ("SortFramePeriod", "{x: 8, y: 8, z: 8, w: 16}"),
        # ROUND 6 (research sortfeel + lite_sortfeel frac 8, Docs/SWARM_FAUNA.md §12)
        ("SortWellDead", "0.7"), ("SortWellDeadTime", "0"), ("SortWander", "0.05"), ("SortWanderTau", "12"),
        ("SortUpdateFraction", "8"),
        # ROUND 11d (the post-cull jolt, Docs/SWARM_FAUNA.md §22)
        ("SortBudAtWound", "1"), ("SortFateNear", "1"), ("SortLayRampSeconds", "12")]
FLORA_GROWTH_PER_OFFSPRING = 0.8   # x the plant's own budget: a plant seeds a neighbour as it completes
FLORA_COOLDOWN = 20
FLORA_SPREAD = 120

# ladder ratios (against the modelled mature CELL; see ladder()). Round 7 left the GPU-drawn bodies out of
# LiveVolume; round 8 (Docs/SWARM_FAUNA.md §16.3) puts them back, and round 11a (§19) makes every member body a
# PrismSpatialIndex virtual entry bound to the cell (Cell.BindVirtualMass), summed exactly where a fauna body
# prism lands. So the ladder is the forest PLUS
# the three bodies grown full, and the ratios are unchanged.
RESTLESS_ENTER, RESTLESS_EXIT, FRENZY_ENTER, FRENZY_EXIT = 0.35, 0.26, 2.5, 2.2
LATTICE_HEART_COLLIDERS = 1080
COLLIDER_CEILING = 1200         # round 7: always-on hearts + every proxy's two colliders, all swarms engaged
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
SHADER_META = ("fileFormatVersion: 2\nguid: %s\nShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n"
               "  nonModifiableTextures: []\n  preprocessorOverride: 0\n  userData:\n  assetBundleName:\n"
               "  assetBundleVariant:\n")
HLSL_META = ("fileFormatVersion: 2\nguid: %s\nShaderIncludeImporter:\n  externalObjects: {}\n  userData:\n"
             "  assetBundleName:\n  assetBundleVariant:\n")
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


EVO_RULE_SRC = "Tools/NCA/results/live/evo_rule.json"   # the G2 weights the evofate genome leaves unchanged


def evo_rule_path():
    return os.path.join(SWARM_DIR, "SwarmEvoFateRule.json")


def evo_rule_from_research():
    """The trained G2 network as SwarmEvoRule.Parse reads it (hidden, fire_rate, w1..b3 as base64 float32),
    or None when the research ref is not reachable. evofate_model.EvoFate runs these weights unchanged: the
    evo genome's output-gain switch (sw_out) is off - checked here, so a re-export with it on fails loudly."""
    import json
    try:
        text = subprocess.run(["git", "-C", REPO, "show", f"{swarm_plans.RESEARCH_REF}:{EVO_RULE_SRC}"],
                              check=True, capture_output=True, text=True).stdout
    except subprocess.CalledProcessError:
        return None
    d = json.loads(text)
    w = d["weights"]
    shapes = {"w1": [192, 232], "b1": [192], "w2": [192, 192], "b2": [192], "w3": [35, 192], "b3": [35]}
    for k, sh in shapes.items():
        if w[k]["shape"] != sh:
            raise SystemExit(f"{EVO_RULE_SRC}: {k} is {w[k]['shape']}, the core expects {sh}")
    if d["hidden"] != 192:
        raise SystemExit(f"{EVO_RULE_SRC}: hidden {d['hidden']}, the core expects 192")
    body = ",\n".join(f'  "{k}": "{w[k]["b64"]}"' for k in shapes)
    return ("{\n" f'  "source": "{swarm_plans.RESEARCH_REF}:{EVO_RULE_SRC}",\n'
            f'  "hidden": {d["hidden"]},\n  "fire_rate": {d["fire_rate"]},\n' + body + "\n}\n")


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
            # ...and TadPoleFauna's dead overrides of HealthPrism.TargetScale, a field the prism
            # animation rework retired (Unity keeps unresolvable modifications forever; a fresh copy
            # should not inherit them - check_generated_assets.py `override`)
            d = re.sub(r"    - target: \{[^}]*\}\n      propertyPath: TargetScale\.[xyz]\n      value: [^\n]*\n"
                       r"      objectReference: \{fileID: 0\}\n", "", d)
            d = d.replace("    m_RemovedComponents: []\n",
                          "    m_RemovedComponents:\n    - {fileID: -6330896111675949634, guid: 32182c0d19e344b4c9a9b332052037e6,\n"
                          "        type: 3}\n")
        out.append(d)
    text = head + "".join(out)
    text = text.replace(f"--- !u!114 &{TADPOLE_MB_FID}\n", f"--- !u!114 &{TADPOLE_MB_FID}\n", 1)
    return text


def anchor_name(model):
    return {"Field": "SwarmFauna.prefab", "Grid": "SwarmGridFauna.prefab", "Sort": "SwarmSortFauna.prefab",
            "EvoFate": "SwarmEvoFateFauna.prefab"}[model]


def config_name(model):
    return {"Field": "SwarmFaunaConfig.asset", "Grid": "SwarmGridFaunaConfig.asset", "Sort": "SwarmSortFaunaConfig.asset",
            "EvoFate": "SwarmEvoFateFaunaConfig.asset"}[model]


def anchor_prefab(model="Field"):
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
  m_Name: {anchor_name(model)[:-7]}
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
  config: {{fileID: 11400000, guid: {guid(rel(os.path.join(SWARM_DIR, config_name(model))))}, type: 2}}
"""


# ── the swarm config ────────────────────────────────────────────────────────────────────────

def v4(d):
    return "{x: %s, y: %s, z: %s, w: %s}" % tuple(_g(d[k]) for k in ("Charge", "Mass", "Space", "Time"))


def _g(x):
    return ("%.3f" % x).rstrip("0").rstrip(".")


def _g5(x):
    return ("%.5f" % x).rstrip("0").rstrip(".")


def config_asset(eggs, model="Field"):
    egg = {"Charge": eggs[0], "Mass": eggs[1], "Space": eggs[2], "Time": eggs[3]}
    pg = lambda k: guid(rel(plan_path(k)))
    grid = "".join(f"  {k}: {_g(v)}\n" for k, v in GRID.items())
    grid += f"  GridFramePeriod: {v4(GRID_PERIOD)}\n"
    grid += "".join(f"  {k}: {_g(v)}\n" for k, v in GRID_TAIL.items())
    return SO_HEADER % (script_guid("SwarmFaunaConfigSO"), config_name(model)[:-6]) + (
        f"  Model: {MODEL_ID[model]}\n"
        f"  MultiDomain: {MULTI_DOMAIN}\n"
        f"  LineageDrift: {_g(LINEAGE_DRIFT)}\n"
        f"  ChargePlan: {{fileID: 4900000, guid: {pg('charge')}, type: 3}}\n"
        f"  MassPlan: {{fileID: 4900000, guid: {pg('mass')}, type: 3}}\n"
        f"  SpacePlan: {{fileID: 4900000, guid: {pg('space')}, type: 3}}\n"
        f"  TimePlan: {{fileID: 4900000, guid: {pg('time')}, type: 3}}\n"
        f"  TadpolePrefab: {{fileID: {TADPOLE_MB_FID}, guid: {guid(rel(os.path.join(PREFAB_DIR, 'SwarmTadpole.prefab')))}, type: 3}}\n"
        f"  UnitScale: {_g(UNIT_SCALE)}\n  TickHz: {_g(TICK_HZ)}\n  MaxStepsPerFrame: 3\n"
        "  SimBudgetMsPerFrame: 3\n"
        f"  PlanDensity: {PLAN_DENSITY if model == 'Sort' else 1}\n"
        "  SimulateOffMainThread: 1\n  DrawMembersOnGpu: 1\n"
        f"  MemberShader: {{fileID: 4800000, guid: {guid(rel(MEMBER_SHADER))}, type: 3}}\n"
        f"  Theme: {{fileID: 11400000, guid: {THEME}, type: 2}}\n"
        f"  EngageRadius: {ENGAGE_RADIUS}\n  MaxProxies: {MAX_PROXIES}\n  ProxyLingerSeconds: 2\n"
        f"  SeedMembers: {SEED_MEMBERS}\n"
        f"  BiteRadius: 10\n  BitersPerStep: {BITERS_PER_STEP}\n"
        f"  EggVolume: {v4(egg)}\n"
        f"  CrossElementCost: 2\n  StomachEggs: 240\n"
        f"  ForageBelow: {_g(FORAGE[0])}\n  SatedAbove: {_g(FORAGE[1])}\n  GiveUpSeconds: {_g(FORAGE[2])}\n  PlantRestSeconds: {_g(FORAGE[3])}\n"
        f"  LayRate: 0.2\n  LayMax: 16\n  MaxSpawnsPerFrame: {MAX_SPAWNS_PER_FRAME}\n  KillLayHoldSeconds: 2\n"
        "  StarvationSeconds: 90\n  ShedIntervalSeconds: 1\n  ExtinctLingerSeconds: 10\n"
        "  Cruise: 0.35\n  TurnPerStep: 0.03\n  WanderReach: 300\n"
        "  VesselRadius: 9\n  SenseMargin: 400\n  DangerEnter: 0.45\n  DangerExit: 0.15\n"
        f"  HeartWorldScale: {v4(HEARTS)}\n"
        f"  PrismScale: {_g(PRISM_SCALE)}\n  HeartPrismGap: 0.6\n  BirthBloomSeconds: 0.8\n  MoltHeartSeconds: 0.5\n"
        + grid + "".join(f"  {k}: {v}\n" for k, v in SORT) +
        f"  EvoRule: {{fileID: 4900000, guid: {guid(rel(evo_rule_path()))}, type: 3}}\n"
        + "".join(f"  {k}: {_g(v)}\n" for k, v in EVO.items()) +
        "  SwarmLoopEvent:\n" + EMPTY_EVENT.replace("    ", "    ", 1) +
        "  MorphEvent:\n" + EMPTY_EVENT)


# ── the cell ────────────────────────────────────────────────────────────────────────────────

PREFIX = "Swarm"


def cell_path(name):
    return os.path.join(CELL_DIR, name + ".asset")


def fauna_name(r):
    return f"{PREFIX} {r['key']} {r['start']} Swarm Fauna Config Data"


def flora_name(r):
    # the SPECIES is in the name, so author_flora_populations.py hands this config to the species' owner
    # rule ("Borromean"); the CELL generator (this script) authors it, the Wrecking Ball way
    return f"{PREFIX} {r['key']} {r['flora']} Flora {r['food']} Config Data"


def canon_path(r):
    return A("_SO_Assets", "Lifeforms", f"{r['flora']} Flora {r['food']}.asset")


def canon(r):
    """The canonical species config the band forks (Borromean: one per element, a measured table)."""
    path = canon_path(r)
    text = read(path)
    leaf = re.search(r"LeafSize: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+)\}", text).groups()
    budget = int(re.search(r"MaxTotalSpawnedObjects: (\d+)", text).group(1))
    prefab = re.search(r"FloraPrefab: \{fileID: (-?\d+), guid: (\w+)", text).groups()
    return dict(guid=meta_guid(path), leaf=tuple(map(float, leaf)), budget=budget, prefab=prefab, text=text)


def fauna_asset(r):
    lo, hi = r["band"]
    return SO_HEADER % (SO_SCRIPT["fauna"], fauna_name(r)) + (
        f"  FaunaPrefab: {{fileID: {ROOT_MB_FID}, guid: {guid(rel(os.path.join(PREFAB_DIR, anchor_name(r['model']))))}, type: 3}}\n"
        f"  InitialSpawnCount: {r['swarms']}\n  PopulationSize: {r['swarms']}\n  SpawnProbability: 1\n  NetworkSynced: 0\n"
        "  FeedsPerOffspring: 0\n  OffspringPerBirth: 1\n  ReproductionCooldownSeconds: 10\n"
        f"  MaxLivePopulation: {r['swarms']}\n  ReleaseTier: 0\n"
        f"  BandInnerRadius: {lo}\n  BandOuterRadius: {hi}\n  CenterFocusBias: 0\n"
        f"  Element: {ELEMENT_ID[r['start']]}\n"
        "  Variant:\n    Enabled: 0\n"
        "  SpreadElements: 0\n  ElementPalette: []\n")


def flora_band(r):
    """Where a region's flora is planted: its flora_band, else the swarm's band."""
    return r.get("flora_band", r["band"])


def flora_asset(r, c):
    """A FORK of the canonical species config: the species' own plate, budget, quota, heart and shield
    (byte for byte), with only what a CELL owns rewritten - the name, how many (seed floor, live cap) and
    where (the planting band, as the cell-level override pair)."""
    lo, hi = flora_band(r)
    t = c["text"]
    t = re.sub(r"(?m)^  m_Name: .*$", f"  m_Name: {flora_name(r)}", t, count=1)
    t = re.sub(r"(?m)^  InitialSpawnCount: \d+$", f"  InitialSpawnCount: {r['floor']}", t, count=1)
    t = re.sub(r"(?m)^  PopulationSize: \d+$", f"  PopulationSize: {r['floor']}", t, count=1)
    t = re.sub(r"(?m)^  MaxLivePopulation: \d+$", f"  MaxLivePopulation: {r['cap']}", t, count=1)
    t = re.sub(r"(?m)^  PlantRadiusCellFraction(Max|Min)Override: .*\n", "", t)
    t = re.sub(r"(?m)^  SpreadPlanting: .*\n", "", t)
    t = re.sub(r"(?m)^  PlantingPens:.*\n(?:  - .*\n|    .*\n)*", "", t)
    if not t.endswith("\n"):
        t += "\n"
    t += (f"  PlantRadiusCellFractionMaxOverride: {_g(hi / MEMBRANE_RADIUS)}\n"
          f"  PlantRadiusCellFractionMinOverride: {_g(lo / MEMBRANE_RADIUS)}\n"
          # round 11-10: a band's few plants spread over it (Mitchell's best candidate) - one sector pen in two of
          # the Middle shell held no plant drawn at random (FloraConfigurationSO.SpreadPlanting)
          "  SpreadPlanting: 1\n")
    t += pens_yaml(r)
    return t


def pens_yaml(r):
    """The region's planting pens as FloraConfigurationSO.PlantingPens YAML (empty = the whole planting band)."""
    pens = r.get("pens", [])
    if not pens:
        return "  PlantingPens: []\n"
    out = "  PlantingPens:\n"
    for axis, half, lo, hi in pens:
        ax = axis or (0, 0, 0)
        out += (f"  - Axis: {{x: {_g(ax[0])}, y: {_g(ax[1])}, z: {_g(ax[2])}}}\n"
                f"    HalfAngle: {_g(half)}\n"
                f"    InnerFraction: {_g5(lo / MEMBRANE_RADIUS)}\n"
                f"    OuterFraction: {_g5(hi / MEMBRANE_RADIUS)}\n")
    return out


def model(plans):
    """The mature cell, modelled (Docs/SWARM_FAUNA.md §6, §14 say how to re-measure it in the editor).

    A Borromean prism is a PLATE of its element's measured leaf; a plant is its whole site table (it
    closes). The swarms are the three bodies at PLAN_DENSITY x their plan's headcount: they cost no
    always-on collider (a member is GPU-drawn data until a vessel is near), and their bodies are not in
    the cell's LiveVolume (no registered prism) - so the ladder is the forest's."""
    eggs = egg_volumes(plans)
    kind = {"whale": "mass", "pufferfish": "charge", "jellyfish": "space", "dragonfly": "time"}
    rows, tot = [], dict(prisms=0, volume=0.0, hearts=0, plants_max=0, tadpoles=0, bill=0.0, food=0.0, forest_at_cap=0.0,
                         bodies=0.0, min_member=1e30)
    m3 = (2.0 * UNIT_SCALE * PRISM_SCALE) ** 3   # SwarmTickJob.Build: scale = 2 * unit * prismScale * half
    for r in REGIONS:
        c = canon(r)
        per = c["leaf"][0] * c["leaf"][1] * c["leaf"][2]
        p = plans[kind[r["plan"]]]
        mix = [0] * 4
        for e in p["elem"]:
            mix[e] += 1
        food_e = {"Charge": 0, "Mass": 1, "Space": 2, "Time": 3}[r["food"]]
        # the egg bill of the full body: own-element eggs at their price, the rest at the cross price (2x)
        bill = sum(PLAN_DENSITY * mix[e] * 0.939 * eggs[e] * (1 if e == food_e else 2) for e in range(4))
        # round 8: the body's own volume, grown to the sort core's fill (SortBodyFill 0.939) - each tadpole is a
        # body prism of its plan slot's half-extents, as the tick job draws and the cell now counts it
        hf = p["half"]
        unit_vols = [m3 * abs(hf[3 * k] * hf[3 * k + 1] * hf[3 * k + 2]) for k in range(p["n"])]
        body_volume = PLAN_DENSITY * sum(unit_vols) * 0.939 * r["swarms"]
        tot["min_member"] = min(tot["min_member"], min(unit_vols))
        tot["bodies"] += body_volume
        rows.append(dict(r=r, c=c, per=per, plant_volume=per * c["budget"], body=PLAN_DENSITY * p["n"], bill=bill,
                         body_volume=body_volume))
        tot["prisms"] += r["cap"] * c["budget"]
        tot["volume"] += r["cap"] * c["budget"] * per
        tot["plants_max"] += r["cap"]
        tot["tadpoles"] += PLAN_DENSITY * p["n"] * r["swarms"]
        tot["bill"] += bill
        tot["food"] += r["floor"] * c["budget"] * per
        tot["forest_at_cap"] += r["cap"] * c["budget"] * per
    tot["plans"] = plans
    tot["hearts"] = tot["plants_max"]                              # always on: one heart per live plant
    tot["proxy_colliders"] = 2 * MAX_PROXIES * TOTAL_SWARMS        # only near vessels: heart + body each
    # round 11b: the substrate populations' proxies share this cell's ceiling (Tools/Build/author_substrate_fauna.py)
    tot["proxy_colliders"] += substrate().proxy_colliders()
    tot["builder_colliders"] = author_builders.proxy_colliders()   # round 11e: the colonies' proxies, near vessels only
    # round 11b-2: the substrate's bodies are BindVirtualMass volume in LiveVolume, so the ladder sees them
    tot["substrate_bodies"] = substrate().body_volume()
    tot["builder_bodies"] = author_builders.body_volume()
    tot["colliders_engaged"] = tot["hearts"] + tot["proxy_colliders"] + tot["builder_colliders"]
    return rows, tot, eggs


def ladder(tot):
    rt = lambda x, st: int(math.ceil(x / st) * st)
    # proxies + skeletons are counted in prisms beside the forest (a full engagement of every swarm). A member
    # body is VOLUME-only (like every fauna body), so the count ladder does not move in round 8
    prisms = tot["prisms"] + MAX_PROXIES * TOTAL_SWARMS
    # round 11c: plus the substrate populations' bodies (their entries are BindVirtualMass: LiveVolume counts them);
    # round 11-10: plus the builder colonies' member bodies (the same BindVirtualMass entries, author_builders.body_volume)
    volume = tot["volume"] + tot["bodies"] + tot.get("substrate_bodies", 0.0) + tot.get("builder_bodies", 0.0)
    return {
        "RestlessEnter": rt(prisms * RESTLESS_ENTER, 100),
        "RestlessExit": rt(prisms * RESTLESS_EXIT, 100),
        "FrenzyEnter": rt(prisms * FRENZY_ENTER, 100),
        "FrenzyExit": rt(prisms * FRENZY_EXIT, 100),
        "RestlessEnterVolume": rt(volume * RESTLESS_ENTER, 1000),
        "RestlessExitVolume": rt(volume * RESTLESS_EXIT, 1000),
        "FrenzyEnterVolume": rt(volume * FRENZY_ENTER, 1000),
        "FrenzyExitVolume": rt(volume * FRENZY_EXIT, 1000),
    }


def threat_flora():
    """Round 11c (Docs/THREAT_FLORA.md): the threat-flora grove's two species ride this cell's profile, and its
    hearts ride the collider gate. author_threat_flora.py owns those assets (in their own folder - stale() would
    delete them here); this script only lists them. None when that script is absent."""
    try:
        sys.path.insert(0, HERE)
        import author_threat_flora as atf
    except ImportError:
        return None
    return atf


def substrate():
    """Round 11b (Docs/SUBSTRATE_FAUNA.md): the substrate populations live in this cell; their own generator owns them."""
    import author_substrate_fauna
    return author_substrate_fauna


def profile_asset():
    floras = "".join(f"  - {{fileID: 11400000, guid: {guid(rel(cell_path(flora_name(r))))}, type: 2}}\n" for r in REGIONS)
    atf = threat_flora()
    if atf:
        floras += "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in atf.profile_guids())
    faunas = "".join(f"  - {{fileID: 11400000, guid: {guid(rel(cell_path(fauna_name(r))))}, type: 2}}\n" for r in REGIONS)
    faunas += substrate().profile_entries()   # round 11b: the substrate populations (author_substrate_fauna.py owns them)
    faunas += author_builders.profile_entries()   # round 11e: the fortress colony and the thief nest
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


# Round 11g (Docs/ELEMENTAL_ECONOMY.md §4.1): the demo cell plays the TUNED petal burn - one petal
# per element per danger contact instead of five - while every other cell keeps what shipped
# (CellConfigDataSO.PetalBurnRule defaults to Shipped = 0). Flip it here: 0 = Shipped, 1 = Tuned.
PETAL_BURN_RULE = 1

# Round 11h (QA-SWARM-ROUND11-13): the demo cell STARTS HOSTILE. Every creature wears the cell's
# controlling domain (Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md - one colour, the controller's), and
# with nobody in the nucleus the legacy fallback handed control to the local pilot, so solo freestyle
# seeded a friendly cell with no stakes. CellConfigDataSO.initialControllingDomain makes the STARTING
# controller authored data (the InitialFaunaReleaseTier precedent); the nucleus claim still overrides
# it the moment the pilot lays mass there. 0 = Unset (legacy), 1 = OpposingLocalPilot, 101/102/104 =
# a fixed Jade/Ruby/Gold.
INITIAL_CONTROLLING_DOMAIN = 1


def cell_asset(L):
    return SO_HEADER % (SO_SCRIPT["cell"], f"{PREFIX} Cell Config") + (
        "  CellName: Swarm\n"
        "  Description: Three great tadpole swarms in three shells of the cytoplasm - a whale, a\n"
        "    pufferfish and a jellyfish of about a thousand tadpoles each - over Borromean feeding\n"
        "    grounds. Kill a body's majority element and it becomes another animal\n"
        f"  Icon: {{fileID: 21300000, guid: {ICON}, type: 3}}\n"
        "  Difficulty: 2\n  CellEndGameScore: 0\n"
        f"  MembranePrefab: {{fileID: {MEMBRANE[0]}, guid: {MEMBRANE[1]}, type: 3}}\n"
        f"  NucleusPrefab: {{fileID: {NUCLEUS[0]}, guid: {NUCLEUS[1]}, type: 3}}\n"
        f"  CytoplasmPrefab: {{fileID: {CYTOPLASM[0]}, guid: {CYTOPLASM[1]}, type: 3}}\n"
        "  CellModifiers:\n"
        f"  - {{fileID: {MODIFIER[0]}, guid: {MODIFIER[1]}, type: 3}}\n"
        f"  SpawnProfile: {{fileID: 11400000, guid: {guid(rel(cell_path(PREFIX + ' Cell Spawn Profile')))}, type: 2}}\n"
        f"  PetalBurnRule: {PETAL_BURN_RULE}\n"
        f"  initialControllingDomain: {INITIAL_CONTROLLING_DOMAIN}\n"
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
    rule = evo_rule_from_research()
    if rule:
        out[evo_rule_path()] = rule
    out[evo_rule_path() + ".meta"] = TEXT_META % guid(rel(evo_rule_path()))
    for d in (SWARM_DIR, PLAN_DIR, CELL_DIR, SCRIPT_DIR):
        out[d + ".meta"] = FOLDER_META % guid(rel(d))
    for s in SCRIPTS:
        out[os.path.join(SCRIPT_DIR, s + ".cs.meta")] = SCRIPT_META % script_guid(s)
    out[MEMBER_SHADER + ".meta"] = SHADER_META % guid(rel(MEMBER_SHADER))
    out[MEMBER_HLSL + ".meta"] = HLSL_META % guid(rel(MEMBER_HLSL))

    rows, tot, eggs = model(plans)
    prefabs = [("SwarmTadpole.prefab", tadpole_prefab())] + [(anchor_name(m), anchor_prefab(m)) for m in MODEL_ID]
    for name, text in prefabs:
        p = os.path.join(PREFAB_DIR, name)
        out[p] = text
        out[p + ".meta"] = PREFAB_META % guid(rel(p))
    for m in MODEL_ID:
        cfgp = os.path.join(SWARM_DIR, config_name(m))
        out[cfgp] = config_asset(eggs, m)
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


def float32_ulp_check(tot):
    """(smallest member body volume, the largest total the ladder sees with 4x margin, float32 ulp there)."""
    import struct
    top = 4.0 * (tot["volume"] + tot["bodies"] + tot.get("substrate_bodies", 0.0) + tot.get("builder_bodies", 0.0)) * FRENZY_ENTER
    f = struct.unpack("f", struct.pack("f", top))[0]
    ulp = struct.unpack("f", struct.pack("I", struct.unpack("I", struct.pack("f", f))[0] + 1))[0] - f
    return tot["min_member"], top, ulp


def starting_controller_problems(value=None):
    """The authored INITIAL_CONTROLLING_DOMAIN must be a member of the C# enum, and the YAML key this
    script writes must be the field CellConfigDataSO serializes - a renamed field or a dropped member
    would otherwise ship a cell that silently starts friendly again (Unity drops an unknown key and
    reads an unknown enum value as Unset)."""
    value = INITIAL_CONTROLLING_DOMAIN if value is None else value
    problems = []
    enum_src = read(os.path.join(A(), "_Scripts", "Data", "Enums", "InitialControllingDomain.cs"))
    body = re.search(r"enum InitialControllingDomain\s*\{(.*?)\}", enum_src, re.S)
    members = {int(v): k for k, v in re.findall(r"^\s*(\w+)\s*=\s*(\d+)\s*,", body.group(1), re.M)} if body else {}
    if value not in members:
        problems.append(f"INITIAL_CONTROLLING_DOMAIN = {value} is not a member of InitialControllingDomain {sorted(members)}")
    elif members[value] == "Unset":
        problems.append("INITIAL_CONTROLLING_DOMAIN is Unset - the demo cell would seed in the solo pilot's own colour (friendly)")
    so_src = read(os.path.join(A(), "_Scripts", "Utility", "DataContainers", "CellConfigDataSO.cs"))
    if not re.search(r"\[SerializeField\]\s*InitialControllingDomain\s+initialControllingDomain\b", so_src):
        problems.append("CellConfigDataSO no longer serializes 'initialControllingDomain' - the key this script authors")
    return problems


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
    problems += starting_controller_problems()
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
    if any(r["model"] != "Sort" for r in REGIONS):
        problems.append("round 7: every swarm in the cell is a SORT swarm (the only model that passes the research hold)")
    if sum(r["swarms"] for r in REGIONS) != TOTAL_SWARMS:
        problems.append(f"the cell holds {sum(r['swarms'] for r in REGIONS)} swarms, not {TOTAL_SWARMS}")
    # one density for every creature (a swarm MORPHS between them, so a per-creature density would change
    # its headcount on every morph): the biggest body sets the cap, the smaller creatures are smaller
    bodies = {k: PLAN_DENSITY * p["n"] for k, p in tot["plans"].items()}
    if max(bodies.values()) > 1000:
        problems.append(f"the largest body is {max(bodies.values())} tadpoles - round 7 caps a swarm near 1,000")
    if min(row["body"] for row in rows) < 400:
        problems.append("a starting body under 400 tadpoles is not 'substantially filled' (round 7)")
    # round 8: the member bodies now ride Cell.liveVolumeTotal, a float32 re-derived each pass. The smallest
    # body a member can add must stay well above one ulp of the largest value that total reaches (FrenzyEnter
    # with margin), or a member's birth or death could be invisible to the ladder (the Cleave finding)
    smallest, top, ulp = float32_ulp_check(tot)
    if smallest < 16 * ulp:
        problems.append(f"the smallest member body ({smallest:.3f}) is under 16 ulps of float32 at {top:,.0f} ({ulp:.4f})")
    atf = threat_flora()
    grove = atf.always_on_hearts() if atf else 0
    if tot["colliders_engaged"] + grove >= COLLIDER_CEILING:
        problems.append(f"{tot['colliders_engaged']} colliders with every swarm fully engaged + {grove} threat-flora hearts "
                        f">= the ceiling {COLLIDER_CEILING}")
    # food, PER BAND (the swarms are penned apart): the band's forest at its CAP must be able to pay for
    # its body grown to full once (a grazed plant regrows, so this is a floor on the economy, not its ceiling)
    for row in rows:
        at_cap = row["r"]["cap"] * row["plant_volume"]
        if at_cap < row["bill"]:
            problems.append(f"{row['r']['key']}: its forest at cap ({at_cap:,.0f}) cannot pay its {row['r']['plan']}'s egg bill ({row['bill']:,.0f})")
    # the C# defaults of the sort fields are the authored values (an SO created by hand matches the cell)
    so = read(os.path.join(SCRIPT_DIR, "SwarmFaunaConfigSO.cs"))
    for k, v in SORT:
        m = re.search(r"public \w+ %s = ([^;]+);" % k, so)
        if not m:
            problems.append(f"SwarmFaunaConfigSO has no field {k}")
            continue
        cs = re.sub(r"[f\s]|new\(|\)", "", {"true": "1", "false": "0"}.get(m.group(1), m.group(1))).split(",")
        au = re.findall(r"-?[\d.]+", v)
        if [float(x) for x in cs] != [float(x) for x in au]:
            problems.append(f"SwarmFaunaConfigSO.{k} defaults to {m.group(1)} but the cell authors {v}")
    # round 8: MultiDomain is ON for these configs (the Swarm cell owns them) and OFF in the C# default, so a
    # swarm config created anywhere else keeps the one-colour law
    m = re.search(r"public bool MultiDomain = (\w+);", so)
    if not m or m.group(1) != "false":
        problems.append("SwarmFaunaConfigSO.MultiDomain must default to false (the one-colour law everywhere but here)")
    if MULTI_DOMAIN != 1:
        problems.append("the Swarm cell's swarms are MultiDomain (round 9 lineages)")
    if any(r["food"] == "Charge" for r in REGIONS):
        problems.append("a Charge feeding ground is no food at all (armoured leaves)")
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
    print("Swarm cell - three sort swarms in three shells of the cytoplasm (round 7)\n")
    for row in rows:
        r, c = row["r"], row["c"]
        print(f"  {r['key']:<6} {r['band'][0]:>4}-{r['band'][1]:<4}u  {r['model']:<4} starts {r['start']:<6} ({r['plan']:<10}) "
              f"body {row['body']:>4} tadpoles, egg bill {row['bill']:>8,.0f}  grazes {r['flora']} {r['food']:<5} "
              f"x{r['floor']}..{r['cap']}  {c['budget']} plates/plant, {row['plant_volume']:,.0f} volume/plant")
    print(f"\n  egg = one body prism's world volume: " + ", ".join(
        f"{n} {eggs[i]:.1f}" for i, n in enumerate(("Charge", "Mass", "Space", "Time"))))
    print(f"  food: forest {tot['food']:,.0f} at the floor, {tot['forest_at_cap']:,.0f} at the cap, "
          f"against an egg bill of {tot['bill']:,.0f} to grow all three bodies to full")
    print(f"  colliders: BEFORE (round 6) 5,008 always-on at the caps; NOW {tot['hearts']} always-on plant hearts "
          f"+ up to {tot['proxy_colliders']} swarm + {tot['builder_colliders']} builder proxy colliders only while vessels are near "
          f"({tot['colliders_engaged']} worst case, ceiling {COLLIDER_CEILING}); {tot['tadpoles']} tadpoles GPU-drawn, 0 colliders")
    print(f"  mature forest (model): {tot['prisms']:,} prisms, {tot['volume']:,.0f} volume - Atlantis is {ATLANTIS_PRISMS:,}")
    print(f"  round 8: member bodies grown full {tot['bodies']:,.0f} volume beside the forest's {tot['volume']:,.0f} "
          f"(now in LiveVolume, Docs/SWARM_FAUNA.md §16.3); smallest member body {float32_ulp_check(tot)[0]:.2f} vs float32 ulp "
          f"{float32_ulp_check(tot)[2]:.4f} at {float32_ulp_check(tot)[1]:,.0f}")
    print(f"  round 11c: substrate agent bodies (full pools, each halfway to its split) {tot['substrate_bodies']:,.0f} volume, "
          "in LiveVolume through BindVirtualMass - counted by the ladder (Docs/SUBSTRATE_FAUNA.md §7)")
    print(f"  round 11-10: builder colony member bodies (fortress, thieves, wearer hearts at their caps) "
          f"{tot['builder_bodies']:,.1f} volume, BindVirtualMass - counted by the ladder (Docs/BUILDERS_AND_THIEVES.md §10.4)")
    print("  ladder: " + ", ".join(f"{k} {v:,}" for k, v in L.items()))
    print(f"  plans: {'re-baked from ' + swarm_plans.RESEARCH_REF if baked else 'research ref not reachable - committed plans kept'}")


def stale(out):
    """Files in the cell folder this script owns no longer (an earlier round's populations)."""
    owned = {os.path.abspath(p) for p in out} | {os.path.abspath(p) for p in author_builders.BUILDER_CELL_FILES}
    return sorted(os.path.join(CELL_DIR, f) for f in os.listdir(CELL_DIR)
                  if os.path.abspath(os.path.join(CELL_DIR, f)) not in owned) if os.path.isdir(CELL_DIR) else []


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
        drift += [rel(p) + " (stale: no longer authored)" for p in stale(out)]
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
    gone = stale(out)
    for p in gone:
        os.remove(p)
    print(f"\nwrote {len(out)} files, removed {len(gone)} stale cell files")
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
