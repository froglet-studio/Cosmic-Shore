#!/usr/bin/env python3
"""Author THE BEES (Garrett, 2026-10-31; Docs/ECOSYSTEM.md §66) - every asset, from one model.

    python3 Tools/Build/author_bee_assets.py            # write
    python3 Tools/Build/author_bee_assets.py --check    # FAIL on any drift (reads the disk)
    python3 Tools/Build/author_bee_assets.py --self-test

The bees are the first POLLINATOR (FaunaDiet.Pollinator): a fauna whose effect is productive. Their population is
raised by a HIVE, which is a FLORA (BeeHiveFlora) - so a cell grows bees by listing a hive in its spawn profile's
SupportedFloras, and they count against the flora budget, not the fauna one.

What it owns (one owner per file):
  * the .meta of every script in FloraAndFauna/Bees, of that folder and of Tests/Editor/BeeMathTests.cs;
  * Assets/_Prefabs/FloraAndFauna/Bees/BeeFauna.prefab - a CELESTIAL body (Garrett, 2026-11-01: "it isn't actually a bee.
    it is a celestial etherion based life form. it doesn't really have a sense of up"; the /fauna-creation skill). Its body
    is BUILT FROM ITS SPEC, Tools/Build/fauna_kit/specs/bee.py, by Tools/Build/fauna_kit/build.py: a braided Spine trailing
    the Etherion, three Vanes round it at 120 degrees with a plate each, three Ribs round the Spine, and a Sting carrying the
    stinger (one long, skinny danger prism). The rest is TadPoleFauna.prefab's: its root (rotation and scale reset to
    identity: the spec's numbers are world units at root scale 1), its crystal (the Etherion, at the origin), the root script
    swapped to BeeFauna (a Boid that never forages and steers with a carried up), diet Pollinator, no starvation, no
    collision effects, a fresh Netcode hash, the tadpole's loop sound emptied, and BeeVaneDriver beating the vanes. The
    tadpole's own body is removed. `--check` reads the prefab BACK against the spec (build.read_back);
  * Assets/_Prefabs/FloraAndFauna/Bees/BeeHive.prefab - a fork of BorromeanFlora.prefab with the root script
    swapped to BeeHiveFlora, the Borromean-only fields replaced by the comb's, and the four bee configs wired;
  * _SO_Assets/Lifeforms/Bee Fauna <Element>.asset (the four bee species, one per element) and
    _SO_Assets/Lifeforms/Bee Hive Flora <Element>.asset (the four hive species: the element palette and the
    Spawn Matrix bench's source);
  * the Hesperides garden's hive: Cell Configs/Hesperides Cell/Hesperides Bee Hive Flora Config Data.asset (the four
    elements spread from the palette). The garden's spawn profile is Tools/ecosim/gen_hesperides_assets.py's: it lists
    this config by the guid minted here, and --check fails if the shipped profile does not;
  * the bee prefab's entry in Assets/DefaultNetworkPrefabs.asset (a hatched bee is a networked fauna);
  * (retired 2026-11-01: Bee.fbx, the Earth-insect model of round 2, and Tools/Build/bee_model/build_bee.py; the
    celestial body replaced them.)

It does NOT write the Spawn Matrix toy (author_spawn_matrix_roster.py owns it and lists "Bee Hive") or the flora
populations of other species (author_flora_populations.py hands "Bee Hive" to this script by name).
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
SCRIPT_DIR = A("_Scripts", "Controller", "Environment", "FloraAndFauna", "Bees")
SCRIPTS = ["BeeMath", "BeeHiveFlora", "BeeFauna", "BeeVaneDriver"]
PREFAB_DIR = A("_Prefabs", "FloraAndFauna", "Bees")
LIFEFORMS = A("_SO_Assets", "Lifeforms")
HESP_DIR = A("_SO_Assets", "Cell Configs", "Hesperides Cell")
HESP_PROFILE = os.path.join(HESP_DIR, "Hesperides Cell Spawn Profile.asset")
HESP_HIVE = "Hesperides Bee Hive Flora Config Data"
NETWORK = A("DefaultNetworkPrefabs.asset")
TEST = A("_Scripts", "Tests", "Editor", "BeeMathTests.cs")
SPEC_NAME = "bee"                                          # Tools/Build/fauna_kit/specs/bee.py

TADPOLE = A("_Prefabs", "FloraAndFauna", "TadPoleFauna.prefab")
SPINDLE_SCRIPT = "8ec45d1233573f9409107a25ab23b0c9"       # Spindle.cs
BOID_SCRIPT = "19b38987ca9e2974083abe50717ed186"          # Boid.cs
TADPOLE_ROOT_GO = "5119337659833296837"
TADPOLE_ROOT_TR = "5494491424606190975"
TADPOLE_ROOT_MB = "5945480239701989318"                   # the Boid (what a FaunaConfigurationSO points at)
TADPOLE_NETOBJ = "1065634054817553135"
TADPOLE_EMITTER = "6009907184062220078"
BODY_INST = "4754995950771878307"                         # the tadpole's TadpoleSpindle body instance (removed)
BODY_ROOT_CHILD = "147158808990258422"                    # its stripped transform, a child of the root
SPINDLE_PREFAB = "32182c0d19e344b4c9a9b332052037e6"       # Spindles/TadpoleSpindle.prefab

BORROMEAN = A("_Prefabs", "FloraAndFauna", "BorromeanFlora.prefab")
BORROMEAN_SCRIPT = "51d471a2f48426e98a4d4a605b2e6803"
FLORA_ROOT_MB = "8186157953239024492"
FLORA_CONFIG_SCRIPT = "a32a297a7606432885f4d3e1f83bea9a"   # FloraConfigurationSO.cs
FAUNA_CONFIG_SCRIPT = "c778cfbe4dfc4c5c8401e40c17802311"   # FaunaConfigurationSO.cs
ELEMENTS = [("Charge", 1), ("Mass", 2), ("Space", 3), ("Time", 4)]

# ── the model (every number the assets carry; the C# defaults of BeeHiveFlora/BeeFauna say the same) ─────────────
COMB = dict(cellLeaf=(2.4, 2.4, 6.0), cellSpacing=4.2, combSpacing=7.0, cellsPerComb=19, seedCells=12, maxCells=57)
COLONY = dict(minBees=3, cellsPerBee=9, maxBees=8, hatchPeriodScale=0.25, hatchRadius=12, guardRadius=260,
              forageRadius=420, senseSeconds=0.5)
HIVE_GROW_PERIOD = 2
BEE_SPEED = (14, 26)          # min / max (u/s): quicker than a tadpole (10 / 15)
BEE_BEHAVIOUR_RATE = 0.6      # s between flock steering ticks (a tadpole's 1.5)
# the element forms come from the spec (fauna_kit/specs/bee.py "elements"): BaseBodyScale per element, Space eroded
VANE_BEAT = dict(beatsPerSecond=9, strokeDegrees=24)   # the vanes' beat (BeeVaneDriver): the read of a blur
HESPERIDES_HIVES = dict(initial=2, cap=4)
BENCH_HIVE_CAP = 3


def guid(name):
    return hashlib.md5(("cosmicshore-bees:" + name).encode()).hexdigest()


def rel(path):
    return os.path.relpath(path, REPO).replace(os.sep, "/")


def read(path):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        return fh.read()


def file_guid(path):
    return re.search(r"^guid: (\w+)", read(path + ".meta"), re.M).group(1)


def fid(name):
    """A stable 63-bit local fileID for a new object in a prefab."""
    return int(hashlib.md5(("cosmicshore-bees-fid:" + name).encode()).hexdigest()[:15], 16)


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


def prefab_path(name):
    return os.path.join(PREFAB_DIR, name + ".prefab")


def bee_cfg_path(e):
    return os.path.join(LIFEFORMS, f"Bee Fauna {e}.asset")


def hive_cfg_path(e):
    return os.path.join(LIFEFORMS, f"Bee Hive Flora {e}.asset")


def hesp_hive_path():
    return os.path.join(HESP_DIR, HESP_HIVE + ".asset")


def _g(x):
    return ("%.4f" % x).rstrip("0").rstrip(".") if isinstance(x, float) else str(x)


def _v(t):
    return "{x: %s, y: %s, z: %s}" % tuple(_g(float(c)) for c in t)


def armour_period():
    """ElementalArmour.ShieldPeriod, read through author_flora_armour.py (the law's one reader)."""
    sys.path.insert(0, HERE)
    import author_flora_armour as armour
    return armour.armour_period()


def netcode_hash(prefab_guid, component_fid):
    sys.path.insert(0, HERE)
    import author_stoat_assets as stoat   # the one xxhash32 / GlobalObjectId_V1 implementation (self-tested there)
    return stoat.global_object_id_hash(prefab_guid, component_fid)


def replace_once(text, old, new, what):
    if text.count(old) != 1:
        raise SystemExit(f"donor drifted: {what} ({text.count(old)} matches for {old[:60]!r})")
    return text.replace(old, new)


# ── the bee prefab ──────────────────────────────────────────────────────────────────────────────────────────────

def instance_block(t):
    """The body TadpoleSpindle instance and every document that belongs to it (stripped + added), in order."""
    docs = re.split(r"(?m)^(?=--- !u!)", t)
    out = []
    for d in docs:
        if d.startswith(f"--- !u!1001 &{BODY_INST}\n") or f"m_PrefabInstance: {{fileID: {BODY_INST}}}" in d:
            out.append(d)
    added = re.findall(r"addedObject: \{fileID: (\d+)\}", out[0])
    for d in docs:
        if any(d.startswith(f"--- !u!114 &{a}\n") for a in added):
            out.append(d)
    return out, added


def spec():
    sys.path.insert(0, os.path.join(HERE, "fauna_kit"))
    import celestial
    return celestial.load(SPEC_NAME)


def body_kit():
    sys.path.insert(0, os.path.join(HERE, "fauna_kit"))
    import build
    return build


def bee_prefab():
    t = read(TADPOLE)
    pg = guid(rel(prefab_path("BeeFauna")))
    sp = spec()
    kit = body_kit()
    # the tadpole's body goes: its instance, its stripped objects and its added components
    body_docs, _added = instance_block(t)
    if len(body_docs) < 5:
        raise SystemExit("TadPoleFauna.prefab: the body instance this script removes has moved")
    for d in body_docs:
        t = t.replace(d, "", 1)
    t = replace_once(t, f"  - {{fileID: {BODY_ROOT_CHILD}}}\n", "", "the body's place under the root")
    # the root: identity rotation, scale 1 (the spec's numbers are world units at root scale 1; BaseBodyScale scales it)
    root_tr = re.search(r"--- !u!4 &%s\nTransform:\n(?:  .*\n)+?(?=---|\Z)" % TADPOLE_ROOT_TR, t).group(0)
    nr = re.sub(r"  m_LocalRotation: \{[^}]*\}", "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}", root_tr, count=1)
    nr = re.sub(r"  m_LocalScale: \{[^}]*\}", "  m_LocalScale: {x: 1, y: 1, z: 1}", nr, count=1)
    t = t.replace(root_tr, nr, 1)

    t = replace_once(t, "  m_Name: MassTadPoleFauna\n", "  m_Name: BeeFauna\n", "root name")
    t = replace_once(t, f"guid: {BOID_SCRIPT}, type: 3", f"guid: {script_guid('BeeFauna')}, type: 3", "root script")
    body, root_children, parts = kit.build_body(sp, int(TADPOLE_ROOT_TR))
    root = re.search(r"--- !u!114 &%s\nMonoBehaviour:\n(?:  .*\n)+" % TADPOLE_ROOT_MB, t)
    rb = root.group(0)
    nrb = rb
    for k, v in (("diet", "2"), ("starvationSeconds", "0"), ("forager", "0"), ("collisionEffects", "[]"),
                 ("behaviorUpdateRate", _g(BEE_BEHAVIOUR_RATE)), ("minSpeed", str(BEE_SPEED[0])),
                 ("maxSpeed", str(BEE_SPEED[1])),
                 # the Boid's template prism (its nest builder's) is the first Rib, a health prism of the body
                 ("healthPrism", f"{{fileID: {parts['Rib0']['hp']}}}")):
        nrb, n = re.subn(r"(?m)^  %s: [^\n]*$" % k, f"  {k}: {v}", nrb, count=1)
        if n != 1:
            raise SystemExit(f"TadPoleFauna.prefab: the Boid field {k} has moved")
    t = t.replace(rb, nrb, 1)
    # a fresh Netcode identity (a copied hash collides with the tadpole)
    t, n = re.subn(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % TADPOLE_NETOBJ,
                   lambda m: f"{m.group(1)}{netcode_hash(pg, TADPOLE_NETOBJ)}\n", t, count=1)
    if n != 1:
        raise SystemExit("TadPoleFauna.prefab: the NetworkObject has moved")
    # the loop sound ships empty (wired by the audio owner; never a placeholder)
    em = re.search(r"--- !u!114 &%s\nMonoBehaviour:\n(?:  .*\n|    .*\n|      .*\n)+" % TADPOLE_EMITTER, t).group(0)
    nem = re.sub(r"  EventReference:\n    Guid:\n      Data1: -?\d+\n      Data2: -?\d+\n      Data3: -?\d+\n      Data4: -?\d+\n"
                 r"    Path: [^\n]*\n",
                 "  EventReference:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n", em)
    t = t.replace(em, nem, 1)

    # the vane driver on the root, and the body under it
    vd = str(fid("vane-driver"))
    t = replace_once(t, f"  - component: {{fileID: {TADPOLE_ROOT_MB}}}\n",
                     f"  - component: {{fileID: {TADPOLE_ROOT_MB}}}\n  - component: {{fileID: {vd}}}\n", "root components")
    children = re.search(r"(--- !u!4 &%s\nTransform:\n(?:.*\n)*?  m_Children:\n)((?:  - \{fileID: -?\d+\}\n)+)" % TADPOLE_ROOT_TR, t)
    t = t[:children.end(2)] + "".join(f"  - {{fileID: {x}}}\n" for x in root_children) + t[children.end(2):]
    vanes = [s_["name"] for s_ in sp["spindles"] if s_["name"].startswith("Vane")]
    driver = (f"--- !u!114 &{vd}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
              f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {TADPOLE_ROOT_GO}}}\n"
              f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {script_guid('BeeVaneDriver')}, type: 3}}\n"
              f"  m_Name: \n  m_EditorClassIdentifier: \n  vanes:\n" + "".join(f"  - {v}\n" for v in vanes) +
              f"  beatsPerSecond: {VANE_BEAT['beatsPerSecond']}\n  strokeDegrees: {VANE_BEAT['strokeDegrees']}\n")
    if not t.endswith("\n"):
        t += "\n"
    return t + driver + body


# ── the hive prefab ─────────────────────────────────────────────────────────────────────────────────────────────

def hive_prefab():
    t = read(BORROMEAN)
    root = f"--- !u!114 &{FLORA_ROOT_MB}\n"
    if root not in t or BORROMEAN_SCRIPT not in t:
        raise SystemExit("BorromeanFlora.prefab: the root MonoBehaviour this script forks has moved")
    t = t.replace(f"guid: {BORROMEAN_SCRIPT}, type: 3", f"guid: {script_guid('BeeHiveFlora')}, type: 3", 1)
    t = re.sub(r"(?m)^  m_Name: BorromeanFlora$", "  m_Name: BeeHive", t, count=1)
    t = re.sub(r"(?m)^      value: BorromeanCrystal$", "      value: BeeHiveCrystal", t, count=1)
    t = re.sub(r"(?m)^  healthBlocksForMaturity: \d+$", f"  healthBlocksForMaturity: {COMB['seedCells']}", t, count=1)
    t = re.sub(r"(?m)^  minHealthBlocks: -?\d+$", "  minHealthBlocks: -1", t, count=1)
    t = re.sub(r"(?m)^  leafSize: \{[^}]*\}$", f"  leafSize: {_v(COMB['cellLeaf'])}", t, count=1)
    t = re.sub(r"(?m)^  growPeriod: [\d.]+$", f"  growPeriod: {HIVE_GROW_PERIOD}", t, count=1)
    t = re.sub(r"(?m)^  plantRadiusCellFraction: [\d.]+$", "  plantRadiusCellFraction: 0.8", t, count=1)
    t = re.sub(r"(?m)^  plantRadiusCellFractionMin: [\d.]+$", "  plantRadiusCellFractionMin: 0.35", t, count=1)
    configs = "".join(f"  - {{fileID: 11400000, guid: {guid(rel(bee_cfg_path(e)))}, type: 2}}\n" for e, _ in ELEMENTS)
    hive = (f"  cellLeaf: {_v(COMB['cellLeaf'])}\n  cellSpacing: {_g(COMB['cellSpacing'])}\n"
            f"  combSpacing: {_g(COMB['combSpacing'])}\n  cellsPerComb: {COMB['cellsPerComb']}\n"
            f"  seedCells: {COMB['seedCells']}\n  maxCells: {COMB['maxCells']}\n  beeConfigs:\n{configs}"
            + "".join(f"  {k}: {_g(v)}\n" for k, v in COLONY.items())
            + "  hiveGrowSfx:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n")
    t, n = re.subn(r"(?m)^  maxTotalSpawnedObjects: \d+\n  surfaceScale: [\d.]+\n", hive, t, count=1)
    if n != 1:
        raise SystemExit("BorromeanFlora.prefab: its Borromean-only fields have moved")
    return t


# ── the configs ─────────────────────────────────────────────────────────────────────────────────────────────────

def bee_config(e, value):
    pg = guid(rel(prefab_path("BeeFauna")))
    form = spec()["elements"][e]   # the spec's element table (/fauna-creation L8)
    name = f"Bee Fauna {e}"
    return SO_HEADER % (FAUNA_CONFIG_SCRIPT, name) + (
        f"  FaunaPrefab: {{fileID: {TADPOLE_ROOT_MB}, guid: {pg}, type: 3}}\n"
        # raised by a hive, never seeded or bred by the fauna spawner: no seed count, no feeds, no cap of its own
        "  InitialSpawnCount: 0\n  PopulationSize: 1\n  SpawnProbability: 1\n  FeedsPerOffspring: 0\n"
        "  OffspringPerBirth: 1\n  ReproductionCooldownSeconds: 10\n  MaxLivePopulation: 0\n"
        f"  Element: {value}\n"
        "  Variant:\n    Enabled: 1\n    HeartWorldScale: 0\n"
        f"    BaseBodyScale: {_g(float(form['body_scale']))}\n"
        # zero: every body prism keeps its OWN authored size (one BodyPrismScale would make the stinger a rib)
        "    BodyPrismScale: {x: 0, y: 0, z: 0}\n"
        # Space: the spindles eroded where thickest, the prisms refit as needles (FaunaElementalForm; spec "elements")
        + ("    ErodeSpindle: 1\n" if form.get("erode_spindle") else "") +
        "    BodyMaterial: {fileID: 0}\n"
        "    StarvationSeconds: 0\n    Forager: 2\n"
        "    CohesionRadius: 90\n"
        f"    BehaviorUpdateRate: {_g(BEE_BEHAVIOUR_RATE)}\n"
        "    TrailBlockInteractionRadius: -1\n    SeparationRadius: 40\n    FeedingClusterRadius: -1\n"
        "    GoalWeight: 6\n"
        f"    MinSpeed: {BEE_SPEED[0]}\n    MaxSpeed: {BEE_SPEED[1]}\n"
        "    OverrideAudio: 1\n    AudioLoopEvent:\n      Guid:\n        Data1: 0\n        Data2: 0\n        Data3: 0\n"
        "        Data4: 0\n      Path: \n    AudioMinDistance: -1\n    AudioMaxDistance: -1\n")


def flora_config(name, value, initial, cap, palette=None):
    hg = guid(rel(prefab_path("BeeHive")))
    text = SO_HEADER % (FLORA_CONFIG_SCRIPT, name) + (
        f"  FloraPrefab: {{fileID: {FLORA_ROOT_MB}, guid: {hg}, type: 3}}\n"
        f"  SpawnProbability: 1\n  InitialSpawnCount: {initial}\n  OverrideDefaultPlantPeriod: 0\n"
        f"  NewPlantPeriod: 9999999\n  PopulationSize: {initial}\n  MaxLivePopulation: {cap}\n"
        # a hive buds a daughter (a swarm) for every full comb it grows - the one flora offspring path
        f"  GrowthPerOffspring: {COMB['maxCells']}\n  OffspringPerBirth: 1\n  ReproductionCooldownSeconds: 60\n"
        "  MaturityFraction: 1\n  OffspringSpread: 220\n"
        f"  Element: {value}\n"
        "  Variant:\n    Enabled: 1\n    HeartWorldScale: 0\n"
        "    LeafSize: {x: 0, y: 0, z: 0}\n    GrowPeriod: -1\n    LatticeScale: -1\n"
        # the armour law states Mass's cadence on the asset (author_flora_armour.py); Charge authors none
        f"    ShieldPeriod: {_g(armour_period()) if value == 2 else '-1'}\n"
        "    WitherRingInterval: -1\n    MaxTotalSpawnedObjects: -1\n    MaxTotalSpawnedObjectsScale: -1\n"
        "    ItemsPerGrow: -1\n    RandomItems: -1\n    MaturationSeconds: -1\n    MaxSpawnsPerFrame: -1\n"
        "    PlantRadiusCellFraction: -1\n    PlantRadiusCellFractionMin: -1\n")
    if palette:
        text += "  SpreadElements: 1\n  ElementPalette:\n" + "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in palette)
    return text


def network_with_bee(text):
    pg = guid(rel(prefab_path("BeeFauna")))
    if pg in text:
        return text
    entry = (f"  - Override: 0\n    Prefab: {{fileID: {TADPOLE_ROOT_GO}, guid: {pg},\n      type: 3}}\n"
             "    SourcePrefabToOverride: {fileID: 0}\n    SourceHashToOverride: 0\n    OverridingTargetPrefab: {fileID: 0}\n")
    m = re.search(r"(?m)^  List:\n", text)
    if not m:
        raise SystemExit("DefaultNetworkPrefabs.asset: no List")
    return text[:m.end()] + entry + text[m.end():]


def emit():
    out = {}
    for folder in (SCRIPT_DIR, PREFAB_DIR):
        out[folder + ".meta"] = FOLDER_META % guid(rel(folder))
    for s in SCRIPTS:
        out[os.path.join(SCRIPT_DIR, s + ".cs.meta")] = SCRIPT_META % script_guid(s)
    out[TEST + ".meta"] = SCRIPT_META % guid(rel(TEST))
    for name, text in (("BeeFauna", bee_prefab()), ("BeeHive", hive_prefab())):
        p = prefab_path(name)
        out[p] = text
        out[p + ".meta"] = PREFAB_META % guid(rel(p))
    for e, value in ELEMENTS:
        out[bee_cfg_path(e)] = bee_config(e, value)
        out[hive_cfg_path(e)] = flora_config(f"Bee Hive Flora {e}", value, 1, BENCH_HIVE_CAP)
    palette = [guid(rel(hive_cfg_path(e))) for e, _ in ELEMENTS]
    out[hesp_hive_path()] = flora_config(HESP_HIVE, 2, HESPERIDES_HIVES["initial"], HESPERIDES_HIVES["cap"], palette)
    for p in [bee_cfg_path(e) for e, _ in ELEMENTS] + [hive_cfg_path(e) for e, _ in ELEMENTS] + [hesp_hive_path()]:
        out[p + ".meta"] = ASSET_META % guid(rel(p))
    out[NETWORK] = network_with_bee(read(NETWORK))
    return out


# ── verification ────────────────────────────────────────────────────────────────────────────────────────────────

def colliders():
    """At the caps: hive comb prisms + hive hearts (flora; LOD-culled prisms) and bee bodies + bee hearts."""
    hives = HESPERIDES_HIVES["cap"]
    bees = hives * COLONY["maxBees"]
    per_bee = len(spec()["prisms"])
    return dict(hives=hives, comb=hives * COMB["maxCells"], hive_hearts=hives, bees=bees, bee_prisms=bees * per_bee,
                bee_hearts=bees, per_bee=per_bee)


def verify(out):
    problems = []
    minted = {re.search(r"^guid: (\w+)", t, re.M).group(1): p for p, t in out.items() if p.endswith(".meta")}
    for g, p in minted.items():
        owners = subprocess.run(["grep", "-rl", "--include=*.meta", f"^guid: {g}", A()], capture_output=True, text=True).stdout.splitlines()
        owners = [o for o in owners if os.path.abspath(o) != os.path.abspath(p)]
        if owners:
            problems.append(f"guid {g} of {rel(p)} is already owned by {rel(owners[0])}")
    for s in SCRIPTS:
        if not os.path.exists(os.path.join(SCRIPT_DIR, s + ".cs")):
            problems.append(f"no script {s}.cs for the meta this script owns")
    for name in ("BeeFauna", "BeeHive"):
        t = out[prefab_path(name)]
        fids = re.findall(r"(?m)^--- !u!\d+ &(-?\d+)", t)
        if len(fids) != len(set(fids)):
            problems.append(f"{name}.prefab has a duplicate fileID")
        for ref in set(re.findall(r"\{fileID: (-?\d+)\}", t)) - {"0"}:
            if ref not in fids:
                problems.append(f"{name}.prefab references a local fileID {ref} it does not contain")
    bee = out[prefab_path("BeeFauna")]
    for want, why in ((f"guid: {script_guid('BeeFauna')}", "runs BeeFauna"), ("  diet: 2\n", "is a Pollinator"),
                      ("  forager: 0\n", "never forages"), ("  starvationSeconds: 0\n", "never starves"),
                      (f"guid: {script_guid('BeeVaneDriver')}", "beats its vanes")):
        if want not in bee:
            problems.append(f"BeeFauna.prefab {why}: missing {want.strip()}")
    if BOID_SCRIPT in bee:
        problems.append("BeeFauna.prefab still runs Boid")
    if f"guid: {SPINDLE_PREFAB}" in bee or f"&{BODY_INST}" in bee:
        problems.append("BeeFauna.prefab still carries the tadpole's body")
    # the celestial laws hold on the spec, and the prefab IS the spec (read back from its own transforms)
    sys.path.insert(0, os.path.join(HERE, "fauna_kit"))
    import celestial
    sp = spec()
    problems += [f"spec: {e}" for e in celestial.check(sp)]
    _world, back = body_kit().read_back(bee, sp)
    problems += [f"BeeFauna.prefab: {e}" for e in back]
    root = re.search(r"--- !u!4 &%s\nTransform:\n(?:  .*\n)+?(?=---|\Z)" % TADPOLE_ROOT_TR, bee)
    if not root or "m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}" not in root.group(0) or "m_LocalScale: {x: 1, y: 1, z: 1}" not in root.group(0):
        problems.append("BeeFauna.prefab: the root is not identity rotation and scale 1 (the tadpole's root leaked in)")
    for e, _ in ELEMENTS:
        form, cfg = sp["elements"][e], out[bee_cfg_path(e)]
        if f"    BaseBodyScale: {_g(float(form['body_scale']))}\n" not in cfg:
            problems.append(f"Bee Fauna {e}: BaseBodyScale is not the spec's {form['body_scale']}")
        if bool(form.get("erode_spindle")) != ("    ErodeSpindle: 1\n" in cfg):
            problems.append(f"Bee Fauna {e}: ErodeSpindle is not the spec's ({bool(form.get('erode_spindle'))})")
    for gone in ("Bee.fbx", "Bee.fbx.meta"):
        if os.path.exists(A("_Models", "Fauna", gone)):
            problems.append(f"Assets/_Models/Fauna/{gone} still exists (the Earth-insect model is retired)")
    tad_hash = re.search(r"GlobalObjectIdHash: (\d+)", read(TADPOLE)).group(1)
    if f"GlobalObjectIdHash: {tad_hash}\n" in bee:
        problems.append("BeeFauna.prefab copies the tadpole's Netcode hash (they would collide)")
    hive = out[prefab_path("BeeHive")]
    if BORROMEAN_SCRIPT in hive or "surfaceScale" in hive:
        problems.append("BeeHive.prefab still carries BorromeanFlora")
    for e, _ in ELEMENTS:
        if guid(rel(bee_cfg_path(e))) not in hive:
            problems.append(f"BeeHive.prefab does not wire the {e} bee")
    # the C# defaults are the model's (a hive made by hand in the inspector matches the shipped one)
    cs = read(os.path.join(SCRIPT_DIR, "BeeHiveFlora.cs"))
    for k in ("cellsPerComb", "seedCells", "maxCells", "minBees", "cellsPerBee", "maxBees"):
        m = re.search(r"\b(?:int) %s = (\d+);" % k, cs)
        want = COMB.get(k, COLONY.get(k))
        if not m or int(m.group(1)) != want:
            problems.append(f"BeeHiveFlora.{k} does not default to the model's {want}")
    if COMB["seedCells"] >= COMB["maxCells"]:
        problems.append("the seed comb fills the whole hive: no cell would ever be built from brought-home mass")
    if HESPERIDES_HIVES["cap"] < HESPERIDES_HIVES["initial"]:
        problems.append("the Hesperides hive cap is below its seed count")
    profile = out.get(HESP_PROFILE) or read(HESP_PROFILE)
    if f"guid: {guid(rel(hesp_hive_path()))}, type: 2}}" not in profile.split("SupportedFaunas:")[0]:
        problems.append("the Hesperides profile does not list the hive in SupportedFloras "
                        "(Tools/ecosim/gen_hesperides_assets.py owns that file and lists it)")
    return problems


def moved_prism(prefab, name, by=0.5):
    """The prefab with one prism instance's local x nudged (a planted defect for --self-test)."""
    docs = re.split(r"(?m)^(?=--- !u!)", prefab)
    for i, d in enumerate(docs):
        if d.startswith("--- !u!1001 ") and f"      value: {name}\n" in d:
            docs[i] = re.sub(r"(propertyPath: m_LocalPosition\.x\n      value: )(-?[\d.e]+)",
                             lambda m: m.group(1) + str(float(m.group(2)) + by), d, count=1)
    return "".join(docs)


def self_test():
    out = emit()
    probs = verify(out)
    if probs:
        print("self-test: the fresh emit fails its own checks:\n  - " + "\n  - ".join(probs))
        return 1
    caught = 0
    planted = [
        ("tadpole hash kept", lambda o: o.__setitem__(prefab_path("BeeFauna"), re.sub(
            r"GlobalObjectIdHash: \d+", "GlobalObjectIdHash: " + re.search(r"GlobalObjectIdHash: (\d+)", read(TADPOLE)).group(1),
            o[prefab_path("BeeFauna")], count=1))),
        ("a wing reference dangles", lambda o: o.__setitem__(prefab_path("BeeFauna"),
            o[prefab_path("BeeFauna")] + "  stray: {fileID: 123456789}\n")),
        ("forager on", lambda o: o.__setitem__(prefab_path("BeeFauna"), o[prefab_path("BeeFauna")].replace("  forager: 0\n", "  forager: 1\n"))),
        ("a prism moved off the spec", lambda o: o.__setitem__(prefab_path("BeeFauna"), moved_prism(o[prefab_path("BeeFauna")], "VanePlate0"))),
        ("the stinger made a health prism", lambda o: o.__setitem__(prefab_path("BeeFauna"), o[prefab_path("BeeFauna")].replace(
            "aad956930fb933f4b82b992b1bc2b773", "4a31e01f8b69a584ca245a1687beb4c2"))),
        ("Space not eroded", lambda o: o.__setitem__(bee_cfg_path("Space"), o[bee_cfg_path("Space")].replace("    ErodeSpindle: 1\n", ""))),
        ("hive unlisted", lambda o: o.__setitem__(HESP_PROFILE, read(HESP_PROFILE).replace(
            f"  - {{fileID: 11400000, guid: {guid(rel(hesp_hive_path()))}, type: 2}}\n", ""))),
    ]
    for name, plant in planted:
        o = dict(out)
        plant(o)
        if verify(o):
            caught += 1
        else:
            print(f"self-test: planted '{name}' was NOT caught")
    print(f"self-test: {caught}/{len(planted)} planted defects caught")
    return 0 if caught == len(planted) else 1


def report():
    c = colliders()
    print("The bees (Docs/ECOSYSTEM.md §66)\n")
    print(f"  hive: comb of {COMB['maxCells']} cells ({COMB['cellsPerComb']} per comb), {COMB['seedCells']} grown as a plant, "
          f"the rest built from brought-home mass; bees {COLONY['minBees']}..{COLONY['maxBees']} (+1 per {COLONY['cellsPerBee']} cells)")
    print(f"  Hesperides: {HESPERIDES_HIVES['initial']} hives seeded, cap {HESPERIDES_HIVES['cap']}, elements spread")
    print(f"  colliders at the caps: {c['comb']} comb prisms + {c['hive_hearts']} hive hearts (flora), "
          f"{c['bee_prisms']} bee prisms ({c['per_bee']} a bee) + {c['bee_hearts']} bee hearts ({c['bees']} bees)")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    out = emit()
    problems = verify(out)
    report()
    if args.check:
        drift = [rel(p) for p, t in out.items() if not os.path.exists(p) or read(p) != t]
        if problems or drift:
            print("\nFAIL")
            for p in problems + [f"differs from what this script authors: {x}" for x in drift]:
                print("  - " + p)
            return 1
        print("\nOK - every bee asset matches the model and Hesperides lists the hive.")
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
