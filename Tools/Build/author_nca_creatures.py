#!/usr/bin/env python3
"""Author the NCA CREATURES - the lab's trained 3D neural-cellular-automaton animals, as game fauna.   (Docs/NCA_CREATURES.md)

    python3 Tools/Build/author_nca_creatures.py            # write
    python3 Tools/Build/author_nca_creatures.py --check    # FAIL on any drift (reads the disk)

What it owns, per species in SPECIES (one row per animal; the lizard today, the whale and the jellyfish once their
trainings finish - add a row, run this, and the same runtime grows them):
  * <Species>NcaWeights.json - the trained network re-packed for NcaVoxelWeights.Parse (base64 float32, row-major
    [out, in]) with the body frame (forward, offset), the render tint and the harness-measured grown size;
  * <Species>NcaConfig.asset - the NcaCreatureConfigSO every individual reads (every gameplay number);
  * Nca<Species>Fauna.prefab - the creature: one GameObject with NcaCreatureFauna (its heart is provisioned from the
    element at spawn, its body is the NCA's voxels drawn as prism entities - no authored mesh, no collider);
  * Nca<Species> <Element> Fauna Config Data.asset x4 - the FaunaConfigurationSOs (one per element) a spawn profile
    or the Spawn Matrix lists;
  * the .meta of every script, asset and prefab (stable guids, one owner each).
And for the Swarm demo cell (author_swarm_fauna.py composes these through cell_profile_entries / colliders):
  * Swarm <Band> Nca<Species> Fauna Config Data.asset - the cell's population of the species, penned in a band
    over the flora it eats.

The weights come from the research branch (RESEARCH_REF) when it is reachable; otherwise the committed weights are
kept and checked for shape only - exactly how swarm_plans.py treats the swarm's body plans.

The grown size (GrownVoxels) and the steps to grow it (GrowSteps) are MEASURED by Tools/Build/nca_creature_harness
(`run.sh measure`); the harness asserts the numbers written here still hold.
"""
import argparse
import base64
import hashlib
import json
import math
import os
import struct
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
assert os.path.isdir(os.path.join(REPO, "Assets")), REPO

A = lambda *p: os.path.join(REPO, "Assets", *p)
RESEARCH_REF = "origin/cece/gifted-curie-x2cpd0"
RESEARCH_DIR = "Tools/NCA/results"
SO_DIR = A("_SO_Assets", "NCA Creatures")
PREFAB_DIR = A("_Prefabs", "FloraAndFauna")
SCRIPT_DIR = A("_Scripts", "Controller", "Environment", "FloraAndFauna", "NcaCreature")
SCRIPTS = ["NcaVoxelCore", "NcaCreatureConfigSO", "NcaCreatureFauna"]

ELEMENTS = [("Charge", 1), ("Mass", 2), ("Space", 3), ("Time", 4)]

# One row per animal. `run` is the research export (Tools/NCA/results/<run>/weights.json); `forward` / `offset` are the
# body frame the lab runtime uses for it (nca_creature.src.js; export_nca3d_creature.py); `grown` / `grow_steps` are
# what Tools/Build/nca_creature_harness measured (`run.sh measure`), and it re-asserts them.
SPECIES = [
    dict(name="Lizard", run="lizard3d_swim", forward=(-0.6148, -0.7887), offset=(-1.39, -0.72), tint=0.65,
         grown=1143, grow_steps=50, length_voxels=44, voxel_size=1.2,
         # its population in the Swarm demo cell: the middle shell, over the Time flora it grazes
         swarm=dict(band="Middle", radii=(690, 840), element="Time", count=1, cap=2)),
    # PENDING - the whale (16x26x39) and the jellyfish (22x22x30) share this runtime unchanged; their trainings are
    # being resumed (whale 4850/6000, jelly 3250/5600 steps). When they finish: export (Tools/NCA/export_nca3d_
    # creature.py), measure their body frame (forward/offset - the lab reuses the lizard's offset for both, which is
    # wrong), add a row here, run `nca_creature_harness/run.sh measure`, then this script.
]


def rel(p):
    return os.path.relpath(p, REPO).replace(os.sep, "/")


def guid(path_rel):
    """Stable guid from the asset's repo path (one owner each, the author_swarm_fauna.py convention)."""
    return hashlib.md5(("nca-creatures:" + path_rel).encode()).hexdigest()


def read(p):
    with open(p, encoding="utf-8") as fh:
        return fh.read()


def research_weights(run):
    """The research export, or None when the research ref is not reachable here."""
    try:
        text = subprocess.run(["git", "-C", REPO, "show", f"{RESEARCH_REF}:{RESEARCH_DIR}/{run}/weights.json"],
                              capture_output=True, text=True, check=True).stdout
    except (subprocess.CalledProcessError, FileNotFoundError):
        return None
    return json.loads(text)


def b64(values):
    return base64.b64encode(struct.pack("<%df" % len(values), *values)).decode("ascii")


def flat(m):
    return [float(x) for row in m for x in row] if m and isinstance(m[0], list) else [float(x) for x in m]


def weights_path(sp):
    return os.path.join(SO_DIR, f"{sp['name']}NcaWeights.json")


def weights_json(sp, w):
    C, HID = int(w["channel_n"]), int(w["hidden"])
    w1, b1, w2, b2 = flat(w["w1"]), flat(w["b1"]), flat(w["w2"]), flat(w["b2"])
    assert len(w1) == HID * 4 * C and len(b1) == HID and len(w2) == C * HID and len(b2) == C, sp["name"]
    out = [
        "{",
        f'  "species": "{sp["name"]}",',
        f'  "source": "{RESEARCH_REF}:{RESEARCH_DIR}/{sp["run"]}/weights.json",',
        f'  "channel_n": {C},',
        f'  "hidden": {HID},',
        f'  "fire_rate": {float(w["fire_rate"])},',
        f'  "D": {int(w["D"])},',
        f'  "H": {int(w["H"])},',
        f'  "W": {int(w["W"])},',
        f'  "forward_x": {sp["forward"][0]},',
        f'  "forward_y": {sp["forward"][1]},',
        f'  "offset_x": {sp["offset"][0]},',
        f'  "offset_y": {sp["offset"][1]},',
        f'  "tint": {sp["tint"]},',
        f'  "grown_voxels": {sp["grown"]},',
        f'  "grow_steps": {sp["grow_steps"]},',
        f'  "w1": "{b64(w1)}",',
        f'  "b1": "{b64(b1)}",',
        f'  "w2": "{b64(w2)}",',
        f'  "b2": "{b64(b2)}"',
        "}",
    ]
    return "\n".join(out) + "\n"


def committed_weights_ok(path):
    """Shape check of a committed weights asset (the research ref is not reachable)."""
    if not os.path.exists(path):
        return [f"{rel(path)} is missing and the research ref {RESEARCH_REF} is not reachable to author it"]
    w = json.loads(read(path))
    C, HID = w["channel_n"], w["hidden"]
    n = lambda k: len(base64.b64decode(w[k])) // 4
    want = {"w1": HID * 4 * C, "b1": HID, "w2": C * HID, "b2": C}
    return [f"{rel(path)}: {k} holds {n(k)} floats, expected {v}" for k, v in want.items() if n(k) != v]


# The platform's assets this script references (one owner each - checked by `check_refs`).
RUNTIME_CELL_DATA = "8d4e8398eedc76c4dadb8604f89b9e1b"           # _SO_Assets/Cell Data/Runtime Cell Data.asset
THEME = "d45a23e6bd2da304988606fba6c97628"                       # _SO_Assets/ThemeManagerDataContainer.asset
HEALTH_BLOCK = "1488a2ac58b2b4c43b14f84206bd9195"                # _Prefabs/Trails/HealthBlock.prefab
HEALTH_BLOCK_PRISM_FID = "6313579230210663873"                   # its HealthPrism component
FAUNA_CONFIG_SCRIPT = "c778cfbe4dfc4c5c8401e40c17802311"         # FaunaConfigurationSO.cs
ROOT_GO_FID, ROOT_TR_FID, ROOT_MB_FID = "7316405550184922001", "7316405550184922002", "7316405550184922003"

# Every creature: one heart (always on) + at most one transient hit prism (only while a weapon resolves a hit).
COLLIDERS_PER_CREATURE = 2

# Feeding and breeding (FaunaConfigurationSO / Fauna). A creature must be able to feed and breed before it dies
# (Garrett, 2026-10-06): it is born fed, its stomach lasts STARVATION_SECONDS, one mouthful (up to three flora
# leaves, ~9 u^3 each in the Swarm cell) is a FEED, and FEEDS_PER_OFFSPRING feeds make one offspring.
STARVATION_SECONDS = 120
STOMACH_CAPACITY = 60
FEEDS_PER_OFFSPRING = 24
REPRODUCTION_COOLDOWN = 60
SPECIES_CAP = 2


def heart_scale(sp):
    """Docs/ECOSYSTEM.md §40: K x bodyDiameter^0.5, with the K author_lifeform_heart_sizes.py solves for the whole
    band (the lizard is not its largest lifeform, so K is unchanged by it); capped at HEART_MAX."""
    sys.path.insert(0, HERE)
    import author_lifeform_heart_sizes as hs
    variants = hs.read_deployment_variants()
    hs.compute_body_diameters(variants)
    k = hs.solve_scale_constant(variants)
    body = sp["length_voxels"] * sp["voxel_size"]
    return round(min(hs.HEART_MAX, k * body ** hs.HEART_EXPONENT), 3), body


TEXT_META = """fileFormatVersion: 2
guid: %s
TextScriptImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


FOLDER_META = ("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
SCRIPT_META = ("fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n"
               "  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n"
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


def script_path(name):
    return os.path.join(SCRIPT_DIR, name + ".cs")


def script_guid(name):
    return guid(rel(script_path(name)))


def config_path(sp):
    return os.path.join(SO_DIR, f"{sp['name']}NcaConfig.asset")


def prefab_path(sp):
    return os.path.join(PREFAB_DIR, f"Nca{sp['name']}Fauna.prefab")


def element_config_path(sp, element):
    return os.path.join(SO_DIR, f"Nca{sp['name']} {element} Fauna Config Data.asset")


def swarm_config_path(sp):
    return os.path.join(SO_DIR, f"Swarm {sp['swarm']['band']} Nca{sp['name']} Fauna Config Data.asset")


def v3(x, y, z):
    return f"{{x: {x}, y: {y}, z: {z}}}"


def config_asset(sp):
    heart, _ = heart_scale(sp)
    vs = sp["voxel_size"]
    body_volume = round(sp["grown"] * vs ** 3, 1)
    weights = f"{{fileID: 4900000, guid: {guid(rel(weights_path(sp)))}, type: 3}}"
    return SO_HEADER % (script_guid("NcaCreatureConfigSO"), f"{sp['name']}NcaConfig") + (
        f"  Weights: {weights}\n"
        f"  VoxelSize: {vs}\n"
        f"  ScaleShape: {v3(1.35, 0.6, 1.55)}\n"
        "  AlphaThreshold: 0.3\n  MaxShown: 1600\n"
        f"  Theme: {{fileID: 11400000, guid: {THEME}, type: 2}}\n"
        "  StepsPerSecond: 20\n  BoltStepsPerSecond: 30\n  InlineStepsPerSecond: 8\n  Segments: 8\n"
        "  SwimSpeed: 14\n  BoltSpeed: 38\n  TurnDegreesPerSecond: 40\n  SlowRadius: 40\n"
        "  StrokeThrust: 0.4\n  FullStrokeBendRate: 0.35\n"
        "  VesselSenseRadius: 120\n  BoltClosingSpeed: 25\n  BoltSeconds: 3\n  VesselBiteReachVoxels: 1.5\n"
        "  VesselBiteCooldown: 0.8\n"
        "  BiteRadiusVoxels: 3.5\n  HitPrismSeconds: 2\n"
        f"  HitPrismPrefab: {{fileID: {HEALTH_BLOCK_PRISM_FID}, guid: {HEALTH_BLOCK}, type: 3}}\n"
        f"  HitPrismScale: {v3(4, 4, 4)}\n"
        f"  BodyVolume: {body_volume}\n  HealShareOfMeal: 0.5\n"
        "  MouthReach: 2\n  MouthRadius: 9\n  FeedInterval: 1.2\n  PrismsPerMouthful: 3\n"
        "  MatureFraction: 0.8\n  DeathFraction: 0.35\n"
        f"  HeartWorldScale: {heart}\n  HeartSeatDepthVoxels: 4\n  WitherSeconds: 3\n  DevourSeconds: 1.2\n")


def prefab(sp):
    name = f"Nca{sp['name']}Fauna"
    cfg = f"{{fileID: 11400000, guid: {guid(rel(config_path(sp)))}, type: 2}}"
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
  m_Name: {name}
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
  m_Script: {{fileID: 11500000, guid: {script_guid('NcaCreatureFauna')}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  cellData: {{fileID: 11400000, guid: {RUNTIME_CELL_DATA}, type: 2}}
  domain: 1
  goalUpdateInterval: 5
  goalUpdateIntervalByAggression:
  - 1
  - 0.55
  - 0.25
  goalOrbitRadius: 0
  diet: 0
  predationImmunitySeconds: 6
  starvationSeconds: {STARVATION_SECONDS}
  stomachCapacity: {STOMACH_CAPACITY}
  config: {cfg}
"""


def fauna_config(sp, name, element_id, count, cap, band=(0, 0)):
    pf = f"{{fileID: {ROOT_MB_FID}, guid: {guid(rel(prefab_path(sp)))}, type: 3}}"
    return SO_HEADER % (FAUNA_CONFIG_SCRIPT, name) + (
        f"  FaunaPrefab: {pf}\n"
        f"  InitialSpawnCount: {count}\n  PopulationSize: {cap}\n  SpawnProbability: 1\n  NetworkSynced: 0\n"
        f"  FeedsPerOffspring: {FEEDS_PER_OFFSPRING}\n  OffspringPerBirth: 1\n"
        f"  ReproductionCooldownSeconds: {REPRODUCTION_COOLDOWN}\n"
        f"  MaxLivePopulation: {cap}\n  ReleaseTier: 0\n"
        f"  BandInnerRadius: {band[0]}\n  BandOuterRadius: {band[1]}\n  CenterFocusBias: 0\n"
        f"  Element: {element_id}\n"
        "  Variant:\n    Enabled: 0\n"
        "  SpreadElements: 0\n  ElementPalette: []\n")


# ── hooks for author_swarm_fauna.py (the Swarm demo cell composes its populations) ──────────────

def cell_profile_entries():
    """The Swarm cell spawn profile's SupportedFaunas lines for the NCA creatures."""
    return "".join(f"  - {{fileID: 11400000, guid: {guid(rel(swarm_config_path(sp)))}, type: 2}}\n"
                   for sp in SPECIES if sp.get("swarm"))


def colliders():
    """Worst case in the Swarm cell: every creature at its cap, each with its heart and its one hit prism."""
    return sum(COLLIDERS_PER_CREATURE * sp["swarm"]["cap"] for sp in SPECIES if sp.get("swarm"))


def body_volume():
    """The creatures' bodies for the Swarm cell's phase ladder: each segment is a BindVirtualMass entry, so the cell's
    LiveVolume counts the body (worst case: every creature at its cap, unwounded)."""
    return sum(sp["grown"] * sp["voxel_size"] ** 3 * sp["swarm"]["cap"] for sp in SPECIES if sp.get("swarm"))


def check_refs():
    """Every platform guid this script points at has exactly one .meta owner (a dangling one deserializes to None)."""
    want = {"Runtime Cell Data": RUNTIME_CELL_DATA, "theme": THEME, "HealthBlock": HEALTH_BLOCK,
            "FaunaConfigurationSO": FAUNA_CONFIG_SCRIPT}
    owners = {g: 0 for g in want.values()}
    for root, _, files in os.walk(os.path.join(REPO, "Assets")):
        for f in files:
            if f.endswith(".meta"):
                with open(os.path.join(root, f), encoding="utf-8", errors="replace") as fh:
                    head = fh.read(200)
                for g in owners:
                    if f"guid: {g}" in head:
                        owners[g] += 1
    return [f"{k} ({g}) has {owners[g]} .meta owners, expected 1" for k, g in want.items() if owners[g] != 1]


def emit():
    """Every file this script owns, as {path: text}, plus problems found while building them."""
    out, problems, baked = {}, [], []
    out[SO_DIR + ".meta"] = FOLDER_META % guid(rel(SO_DIR))
    out[SCRIPT_DIR + ".meta"] = FOLDER_META % guid(rel(SCRIPT_DIR))
    for sc in SCRIPTS:
        out[script_path(sc) + ".meta"] = SCRIPT_META % script_guid(sc)
    for sp in SPECIES:
        for path, text, meta in (
                (config_path(sp), config_asset(sp), ASSET_META),
                (prefab_path(sp), prefab(sp), PREFAB_META)):
            out[path] = text
            out[path + ".meta"] = meta % guid(rel(path))
        for element, eid in ELEMENTS:
            path = element_config_path(sp, element)
            out[path] = fauna_config(sp, os.path.basename(path)[:-6], eid, 1, SPECIES_CAP)
            out[path + ".meta"] = ASSET_META % guid(rel(path))
        if sp.get("swarm"):
            sw = sp["swarm"]
            path = swarm_config_path(sp)
            out[path] = fauna_config(sp, os.path.basename(path)[:-6], dict(ELEMENTS)[sw["element"]], sw["count"],
                                     sw["cap"], sw["radii"])
            out[path + ".meta"] = ASSET_META % guid(rel(path))
    for sp in SPECIES:
        wp = weights_path(sp)
        w = research_weights(sp["run"])
        if w is not None:
            out[wp] = weights_json(sp, w)
            baked.append(sp["name"])
        else:
            problems += committed_weights_ok(wp)
            if os.path.exists(wp):
                out[wp] = read(wp)
        out[wp + ".meta"] = TEXT_META % guid(rel(wp))
    problems += check_refs()
    return out, problems, baked


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    out, problems, baked = emit()
    print(f"NCA creatures: {', '.join(s['name'] for s in SPECIES)}; weights "
          f"{'re-baked from ' + RESEARCH_REF + ' for ' + ', '.join(baked) if baked else 'kept (research ref not reachable)'}")
    if args.check:
        drift = [rel(p) for p, t in out.items() if not os.path.exists(p) or read(p) != t]
        if problems or drift:
            print("\nFAIL")
            for p in problems + [f"differs from what this script authors: {d}" for d in drift]:
                print("  - " + p)
            return 1
        print("\nOK - every NCA creature asset matches what this script authors.")
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
    return 0


if __name__ == "__main__":
    sys.exit(main())
