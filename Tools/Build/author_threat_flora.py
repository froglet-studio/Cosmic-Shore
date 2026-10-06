#!/usr/bin/env python3
"""Author the THREAT-FLORA GROVE in the Swarm cell (round 11c, Docs/THREAT_FLORA.md) - every asset, from one model.

    python3 Tools/Build/author_threat_flora.py            # write
    python3 Tools/Build/author_threat_flora.py --check    # FAIL on any drift (reads the disk)

What it owns:
  * the .meta of every script in FloraAndFauna/ThreatFlora (stable guids, one owner each) and of its folder;
  * SnapTrapFlora.prefab and PhysarumSclerotium.prefab (Assets/_Prefabs/FloraAndFauna/Threat Flora/) - forks of
    BorromeanFlora.prefab with the root script swapped, the Borromean-only fields dropped, the grove config wired
    and the crystal renamed. The crystal (a nested prefab instance with its overrides) is kept byte for byte;
  * Assets/_SO_Assets/Threat Flora/: the grove config (ThreatGroveConfigSO) and the two species configs
    (FloraConfigurationSO) the Swarm cell's spawn profile lists.

It does NOT write the Swarm cell's spawn profile: author_swarm_fauna.py owns that file and lists this grove's two
species through profile_guids() below (one owner per file; its `stale()` would delete anything else in that
folder, which is why these assets live in their own folder). Run both scripts; both --checks must pass.

Every number comes from ThreatGroveDefaults.cs (the C# the harness asserts), read here - never a second copy.
"""
import argparse
import hashlib
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
A = lambda *p: os.path.join(REPO, "Assets", *p)
SCRIPT_DIR = A("_Scripts", "Controller", "Environment", "FloraAndFauna", "ThreatFlora")
SCRIPTS = ["ThreatFloraMath", "SnapTrapCore", "PhysarumCore", "ThreatGroveDefaults", "ThreatGroveConfigSO",
           "ThreatGrove", "SnapTrapFlora", "PhysarumSclerotium"]
PREFAB_DIR = A("_Prefabs", "FloraAndFauna", "Threat Flora")
SO_DIR = A("_SO_Assets", "Threat Flora")
DEFAULTS_CS = os.path.join(SCRIPT_DIR, "ThreatGroveDefaults.cs")
CONFIG_CS = os.path.join(SCRIPT_DIR, "ThreatGroveConfigSO.cs")
DONOR = A("_Prefabs", "FloraAndFauna", "BorromeanFlora.prefab")
DONOR_SCRIPT = "51d471a2f48426e98a4d4a605b2e6803"     # BorromeanFlora.cs
ROOT_MB_FID = "8186157953239024492"
FLORA_CONFIG_SCRIPT = "a32a297a7606432885f4d3e1f83bea9a"   # FloraConfigurationSO.cs
THEME = "d45a23e6bd2da304988606fba6c97628"                 # Assets/_SO_Assets/ThemeManagerDataContainer.asset
ELEMENT_NAME = {1: "Charge", 2: "Mass", 3: "Space", 4: "Time"}   # Data/Enums/Element.cs
HEART_SCALE = {3: 2.298, 4: 1.737}                         # the hearts the Swarm cell's other life wears per element
SLOTS_PER_TRAP = 27
GROVE_NAME = "Swarm Threat Grove Config"
SNAP_NAME = "Swarm Snap Trap Flora Time Config Data"
PHYS_NAME = "Swarm Physarum Flora Space Config Data"


def guid(name):
    return hashlib.md5(("cosmicshore-threat-flora:" + name).encode()).hexdigest()


def rel(path):
    return os.path.relpath(path, REPO).replace(os.sep, "/")


def read(path):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        return fh.read()


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


def script_guid(name):
    return guid(f"{rel(SCRIPT_DIR)}/{name}.cs")


def asset_path(name):
    return os.path.join(SO_DIR, name + ".asset")


def prefab_path(name):
    return os.path.join(PREFAB_DIR, name + ".prefab")


# ── the numbers (read from ThreatGroveDefaults.cs) ─────────────────────────────────────────────────────────────

def defaults():
    cs = read(DEFAULTS_CS)
    num = lambda k: float(re.search(r"public const (?:float|int) %s = ([\d.]+)f?;" % k, cs).group(1))
    vec = lambda k: tuple(float(x) for x in re.search(
        r"public static readonly Vector3 %s = new Vector3\(([\d.]+)f, ([\d.]+)f, ([\d.]+)f\);" % k, cs).groups())
    d = {k: num(k) for k in ("GroveInnerRadius", "GroveOuterRadius", "GroveHalfAngleDegrees", "SnapTrapClumps",
                             "SnapTrapSeedFloor", "SnapTrapCap", "ClumpRadius", "ClumpSpreadDegrees",
                             "RootBitesPerAbsorb", "Sclerotia", "ShellPrisms", "ShellRadius",
                             "PlantedTubesPerSclerotium", "MaxTubes", "SimHz", "TrapRootDepthMin", "TrapRootDepthMax",
                             "HelioConeDegrees", "SnapTrapElement", "PhysarumElement", "OuterSwarmBandEdge",
                             "MembraneRadius")}
    for k in ("StalkLeaf", "LobeLeaf", "ToothLeaf", "TubeLeaf", "ShellLeaf"):
        d[k] = vec(k)
    axis = re.search(r"GroveAxis = Vector3\.Normalize\(new Vector3\(([\d.]+)f, ([\d.]+)f, ([\d.]+)f\)\)", cs).groups()
    d["GroveAxis"] = tuple(float(x) for x in axis)
    return d


def _g(x):
    return ("%.4f" % x).rstrip("0").rstrip(".") if isinstance(x, float) else str(x)


def _v(t):
    return "{x: %s, y: %s, z: %s}" % tuple(_g(float(c)) for c in t)


# ── the hook author_swarm_fauna.py calls ───────────────────────────────────────────────────────────────────────

def profile_guids():
    """The two species configs the Swarm cell's spawn profile lists (author_swarm_fauna.py profile_asset)."""
    return [guid(rel(asset_path(SNAP_NAME))), guid(rel(asset_path(PHYS_NAME)))]


def always_on_hearts():
    """Heart colliders the grove adds at its caps: one crystal per trap, one per sclerotium (§5)."""
    d = defaults()
    return int(d["SnapTrapCap"] + d["Sclerotia"])


def prism_colliders():
    """Body-prism colliders at the caps (LOD-culled like every flora prism; not in the always-on gate)."""
    d = defaults()
    return int(d["SnapTrapCap"] * SLOTS_PER_TRAP + d["MaxTubes"] + d["Sclerotia"] * d["ShellPrisms"])


# ── assets ─────────────────────────────────────────────────────────────────────────────────────────────────────

def grove_asset(d):
    return SO_HEADER % (script_guid("ThreatGroveConfigSO"), GROVE_NAME) + (
        f"  InnerRadius: {_g(d['GroveInnerRadius'])}\n  OuterRadius: {_g(d['GroveOuterRadius'])}\n"
        f"  HalfAngleDegrees: {_g(d['GroveHalfAngleDegrees'])}\n  Axis: {_v(d['GroveAxis'])}\n"
        f"  SnapTrapClumps: {int(d['SnapTrapClumps'])}\n  ClumpRadius: {_g(d['ClumpRadius'])}\n"
        f"  ClumpSpreadDegrees: {_g(d['ClumpSpreadDegrees'])}\n"
        f"  HelioConeDegrees: {_g(d['HelioConeDegrees'])}\n"
        f"  StalkLeaf: {_v(d['StalkLeaf'])}\n  LobeLeaf: {_v(d['LobeLeaf'])}\n  ToothLeaf: {_v(d['ToothLeaf'])}\n"
        f"  RootBitesPerAbsorb: {int(d['RootBitesPerAbsorb'])}\n  SnapTrapHz: 20\n  BudRetrySeconds: 2\n"
        f"  TubeLeaf: {_v(d['TubeLeaf'])}\n  ShellLeaf: {_v(d['ShellLeaf'])}\n  ShellRadius: {_g(d['ShellRadius'])}\n"
        f"  PlantedTubesPerSclerotium: {int(d['PlantedTubesPerSclerotium'])}\n  MaxTubes: {int(d['MaxTubes'])}\n"
        f"  PhysarumHz: {_g(d['SimHz'])}\n  WarmupStepsPerFrame: 3\n  MaxLaysPerFrame: 24\n"
        "  FoodRefreshSeconds: 1\n  MaxFood: 1024\n  FarTimeScale: 0.25\n  FarMargin: 400\n"
        f"  Theme: {{fileID: 11400000, guid: {THEME}, type: 2}}\n")


def flora_asset(name, prefab, floor, cap, element, d):
    lo, hi = d["GroveInnerRadius"] / 1200.0, d["GroveOuterRadius"] / 1200.0
    return SO_HEADER % (FLORA_CONFIG_SCRIPT, name) + (
        f"  FloraPrefab: {{fileID: {ROOT_MB_FID}, guid: {guid(rel(prefab_path(prefab)))}, type: 3}}\n"
        f"  SpawnProbability: 1\n  InitialSpawnCount: {floor}\n  OverrideDefaultPlantPeriod: 0\n"
        f"  NewPlantPeriod: 9999999\n  PopulationSize: {floor}\n  MaxLivePopulation: {cap}\n"
        # 0 = no per-plant growth quota: a snap trap buds from the colony's rhizome (TrySpawnOneOffspring), a
        # sclerotium does not reproduce (the seeder holds the network at its floor)
        "  GrowthPerOffspring: 0\n  OffspringPerBirth: 1\n  ReproductionCooldownSeconds: 5\n  MaturityFraction: 0\n"
        "  OffspringSpread: 60\n"
        f"  Element: {element}\n"
        "  Variant:\n    Enabled: 1\n"
        f"    HeartWorldScale: {HEART_SCALE[element]}\n"
        "    LeafSize: {x: 0, y: 0, z: 0}\n    GrowPeriod: -1\n    LatticeScale: -1\n    ShieldPeriod: -1\n"
        "    WitherRingInterval: -1\n    MaxTotalSpawnedObjects: -1\n    MaxTotalSpawnedObjectsScale: -1\n"
        "    ItemsPerGrow: -1\n    RandomItems: -1\n    MaturationSeconds: -1\n    MaxSpawnsPerFrame: -1\n"
        "    PlantRadiusCellFraction: -1\n    PlantRadiusCellFractionMin: -1\n"
        f"  PlantRadiusCellFractionMaxOverride: {_g(round(hi, 4))}\n"
        f"  PlantRadiusCellFractionMinOverride: {_g(round(lo, 4))}\n")


def fork_prefab(name, script, budget, grow_period, crystal_name):
    """BorromeanFlora.prefab with the root script swapped and the grove wired (see the module docstring)."""
    t = read(DONOR)
    root = f"--- !u!114 &{ROOT_MB_FID}\n"
    if root not in t or DONOR_SCRIPT not in t:
        raise SystemExit("BorromeanFlora.prefab: the root MonoBehaviour this script forks has moved")
    t = t.replace(f"guid: {DONOR_SCRIPT}, type: 3", f"guid: {script_guid(script)}, type: 3", 1)
    t = re.sub(r"(?m)^  m_Name: BorromeanFlora$", f"  m_Name: {name}", t, count=1)
    t = re.sub(r"(?m)^      value: BorromeanCrystal$", f"      value: {crystal_name}", t, count=1)
    t = re.sub(r"(?m)^  healthBlocksForMaturity: \d+$", f"  healthBlocksForMaturity: {budget}", t, count=1)
    # -1: losing prisms never kills by count; a plant dies when its heart is taken or nothing of it is left
    t = re.sub(r"(?m)^  minHealthBlocks: -?\d+$", "  minHealthBlocks: -1", t, count=1)
    t = re.sub(r"(?m)^  growPeriod: [\d.]+$", f"  growPeriod: {grow_period}", t, count=1)
    t = re.sub(r"(?m)^  plantRadiusCellFraction: [\d.]+$", "  plantRadiusCellFraction: 0.9933", t, count=1)
    t = re.sub(r"(?m)^  plantRadiusCellFractionMin: [\d.]+$", "  plantRadiusCellFractionMin: 0.9125", t, count=1)
    t = re.sub(r"(?m)^  maxTotalSpawnedObjects: \d+\n  surfaceScale: [\d.]+\n",
               f"  grove: {{fileID: 11400000, guid: {guid(rel(asset_path(GROVE_NAME)))}, type: 2}}\n", t, count=1)
    return t


def emit():
    d = defaults()
    out = {}
    for folder in (SCRIPT_DIR, PREFAB_DIR, SO_DIR):
        out[folder + ".meta"] = FOLDER_META % guid(rel(folder))
    for s in SCRIPTS:
        out[os.path.join(SCRIPT_DIR, s + ".cs.meta")] = SCRIPT_META % script_guid(s)
    prefabs = {
        "SnapTrapFlora": fork_prefab("SnapTrapFlora", "SnapTrapFlora", SLOTS_PER_TRAP, 3, "SnapTrapCrystal"),
        "PhysarumSclerotium": fork_prefab("PhysarumSclerotium", "PhysarumSclerotium", int(d["ShellPrisms"]), 3,
                                          "SclerotiumCrystal"),
    }
    for name, text in prefabs.items():
        p = prefab_path(name)
        out[p] = text
        out[p + ".meta"] = PREFAB_META % guid(rel(p))
    assets = {
        GROVE_NAME: grove_asset(d),
        SNAP_NAME: flora_asset(SNAP_NAME, "SnapTrapFlora", int(d["SnapTrapSeedFloor"]), int(d["SnapTrapCap"]),
                               int(d["SnapTrapElement"]), d),
        PHYS_NAME: flora_asset(PHYS_NAME, "PhysarumSclerotium", int(d["Sclerotia"]), int(d["Sclerotia"]),
                               int(d["PhysarumElement"]), d),
    }
    for name, text in assets.items():
        p = asset_path(name)
        out[p] = text
        out[p + ".meta"] = ASSET_META % guid(rel(p))
    return out, d


# ── verification ───────────────────────────────────────────────────────────────────────────────────────────────

def swarm_model():
    sys.path.insert(0, HERE)
    import author_swarm_fauna as asf
    plans = asf.committed_plans()
    rows, tot, eggs = asf.model(plans)
    return asf, tot


def verify(out, d):
    problems = []
    minted = {re.search(r"^guid: (\w+)", t, re.M).group(1): p for p, t in out.items() if p.endswith(".meta")}
    for g, p in minted.items():
        owners = subprocess.run(["grep", "-rl", "--include=*.meta", f"^guid: {g}", A()],
                                capture_output=True, text=True).stdout.splitlines()
        owners = [o for o in owners if os.path.abspath(o) != os.path.abspath(p)]
        if owners:
            problems.append(f"guid {g} of {rel(p)} is already owned by {rel(owners[0])}")
    for s in SCRIPTS:
        if not os.path.exists(os.path.join(SCRIPT_DIR, s + ".cs")):
            problems.append(f"no script {s}.cs for the meta this script owns")

    asf, tot = swarm_model()
    # the grove sits OFF the swarms' bands, inside the membrane, outside the nucleus
    top = max(r["band"][1] for r in asf.REGIONS)
    if d["GroveInnerRadius"] <= top:
        problems.append(f"the grove (from {d['GroveInnerRadius']}) overlaps the outer swarm band (to {top})")
    if d["OuterSwarmBandEdge"] != top or d["MembraneRadius"] != asf.MEMBRANE_RADIUS:
        problems.append(f"ThreatGroveDefaults' band edge / membrane ({d['OuterSwarmBandEdge']}, {d['MembraneRadius']}) "
                        f"are not the Swarm cell's ({top}, {asf.MEMBRANE_RADIUS}) - harness S9 checks the wrong rim")
    if d["GroveOuterRadius"] >= asf.MEMBRANE_RADIUS:
        problems.append(f"the grove reaches the membrane ({d['GroveOuterRadius']} >= {asf.MEMBRANE_RADIUS})")
    # the collider budget is a hard gate (Docs/SWARM_FAUNA.md §4.1/§14.3): the swarm cell's worst case + our hearts
    engaged = tot["colliders_engaged"] + always_on_hearts()
    if engaged >= asf.COLLIDER_CEILING:
        problems.append(f"{tot['colliders_engaged']} + {always_on_hearts()} grove hearts = {engaged} colliders >= the ceiling {asf.COLLIDER_CEILING}")
    if d["SnapTrapCap"] < d["SnapTrapSeedFloor"]:
        problems.append("the snap-trap cap is below its seed floor")
    # the ScriptableObject's C# defaults ARE ThreatGroveDefaults (a config made by hand matches the cell's)
    so = read(CONFIG_CS)
    for k in ("StalkLeaf", "LobeLeaf", "ToothLeaf", "TubeLeaf", "ShellLeaf"):
        m = re.search(r"public Vector3 %s = new Vector3\(([\d.]+)f, ([\d.]+)f, ([\d.]+)f\);" % k, so)
        if not m or tuple(float(x) for x in m.groups()) != d[k]:
            problems.append(f"ThreatGroveConfigSO.{k} does not default to ThreatGroveDefaults.{k} {d[k]}")
    m = re.search(r"public Vector3 Axis = new Vector3\(([\d.]+)f, ([\d.]+)f, ([\d.]+)f\);", so)
    if not m or tuple(float(x) for x in m.groups()) != d["GroveAxis"]:
        problems.append("ThreatGroveConfigSO.Axis does not default to ThreatGroveDefaults.GroveAxis")
    # the prefab forks really are forks: new script, no Borromean field, the grove wired, ids intact
    for name in ("SnapTrapFlora", "PhysarumSclerotium"):
        t = out[prefab_path(name)]
        if DONOR_SCRIPT in t:
            problems.append(f"{name}.prefab still runs BorromeanFlora")
        if "surfaceScale" in t or "maxTotalSpawnedObjects" in t:
            problems.append(f"{name}.prefab still carries a BorromeanFlora-only field")
        if guid(rel(asset_path(GROVE_NAME))) not in t:
            problems.append(f"{name}.prefab does not reference the grove config")
        fids = re.findall(r"(?m)^--- !u!\d+ &(-?\d+)", t)
        if len(fids) != len(set(fids)):
            problems.append(f"{name}.prefab has a duplicate fileID")
        for ref in set(re.findall(r"\{fileID: (-?\d+)\}", t)) - {"0"}:
            if ref not in fids:
                problems.append(f"{name}.prefab references a local fileID {ref} it does not contain")
    # the Swarm cell's profile lists both species (author_swarm_fauna.py owns that file)
    prof = asf.cell_path(asf.PREFIX + " Cell Spawn Profile")
    ptext = read(prof) if os.path.exists(prof) else ""
    for g in profile_guids():
        if g not in ptext:
            problems.append(f"{rel(prof)} does not list {g} (run author_swarm_fauna.py)")
    return problems, tot, engaged


def report(d, tot, engaged):
    print("Threat-flora grove in the Swarm cell (round 11c, Docs/THREAT_FLORA.md)\n")
    print(f"  where: rim sector {d['GroveInnerRadius']:.0f}-{d['GroveOuterRadius']:.0f} u, half-angle "
          f"{d['GroveHalfAngleDegrees']:.0f} deg about {d['GroveAxis']} (outside every swarm band, inside the membrane)")
    print(f"  snap traps: {int(d['SnapTrapClumps'])} clumps, seed floor {int(d['SnapTrapSeedFloor'])}, cap "
          f"{int(d['SnapTrapCap'])}, {SLOTS_PER_TRAP} prisms each, rooted {d['TrapRootDepthMin']:.0f}-{d['TrapRootDepthMax']:.0f} u "
          f"inside the rim, turning within {d['HelioConeDegrees']:.0f} deg of the cell centre; Element {ELEMENT_NAME[int(d['SnapTrapElement'])]}")
    print(f"  physarum: {int(d['Sclerotia'])} sclerotia, tube cap {int(d['MaxTubes'])}, {int(d['ShellPrisms'])}-prism "
          f"beat shell each; {d['SimHz']:.0f} Hz; Element {ELEMENT_NAME[int(d['PhysarumElement'])]}")
    print(f"  colliders: +{always_on_hearts()} always-on hearts -> {tot['colliders_engaged']} + {always_on_hearts()} = "
          f"{engaged} worst case (ceiling 1200); body prisms at the caps <= {prism_colliders()} "
          "(LOD-culled flora prisms, like every plant's)")


def stale(out):
    """Files in this script's own asset folders it owns no longer (an earlier authoring's species names)."""
    owned = {os.path.abspath(p) for p in out}
    gone = []
    for folder in (SO_DIR, PREFAB_DIR):
        if os.path.isdir(folder):
            gone += [os.path.join(folder, f) for f in sorted(os.listdir(folder))
                     if os.path.abspath(os.path.join(folder, f)) not in owned]
    return gone


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    out, d = emit()
    problems, tot, engaged = verify(out, d)
    report(d, tot, engaged)
    if args.check:
        drift = [rel(p) for p, t in out.items() if not os.path.exists(p) or read(p) != t]
        drift += [rel(p) + " (stale: no longer authored)" for p in stale(out)]
        if problems or drift:
            print("\nFAIL")
            for p in problems + [f"differs from what this script authors: {x}" for x in drift]:
                print("  - " + p)
            return 1
        print("\nOK - every grove asset matches the model and the Swarm cell lists both species.")
        return 0
    hard = [p for p in problems if "run author_swarm_fauna.py" not in p]
    if hard:
        print("\nFAIL - refusing to write:")
        for p in hard:
            print("  - " + p)
        return 1
    for p, t in out.items():
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(t)
    gone = stale(out)
    for p in gone:
        os.remove(p)
    print(f"\nwrote {len(out)} files, removed {len(gone)} stale")
    if len(hard) != len(problems):
        print("  next: python3 Tools/Build/author_swarm_fauna.py (lists the two species in the Swarm cell's profile)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
