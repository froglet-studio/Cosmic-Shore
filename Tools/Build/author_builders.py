#!/usr/bin/env python3
"""Author the BUILDER COLONIES - the fortress colony, the thief nest and the wearers - and their place in the Swarm cell.
(Docs/BUILDERS_AND_THIEVES.md)

    python3 Tools/Build/author_builders.py            # write
    python3 Tools/Build/author_builders.py --check    # FAIL on any drift (reads the disk)

What it owns:
  * the .meta of every script under FloraAndFauna/Builders (stable guids, one owner each) and the folder's;
  * BuilderFortressColony.prefab / BuilderThiefNest.prefab - the heartless population anchors (BuilderColonyFauna);
  * Assets/_SO_Assets/Builder Colonies/FortressColonyConfig.asset / ThiefNestConfig.asset - every number they run on,
    written from BuilderColonyConfigSO's own C# defaults plus the per-species overrides below (so a default changed
    in C# flows into both assets on the next run, and --check names the drift);
  * the two species configs in the Swarm cell (FaunaConfigurationSO: prefab, count, band, element).
    author_swarm_fauna.py lists them in the cell's spawn profile (it owns the profile) and counts their proxies
    in its collider ceiling; its stale() leaves this script's files alone (BUILDER_CELL_FILES).

WHERE they live - off the swarms' bands (Inner 470-620, Middle 690-840, Outer 910-1080):

    fortress  845-905u   in the gap between the pufferfish and the jellyfish shells
    thieves  1085-1140u  the rim gap; the nest PERCHES on the nearest living plant (Outer Space flora)
    wearers   400-465u   the inner gap between the nucleus (392) and the inner swarm band; the hearts are FOUNDED
                         there and roam the whole cell after a trail (§10: no band filter on what they steal)

Their colour is the cell's controlling domain at spawn (the spawner's rule) - never prescribed here.
"""
import argparse
import hashlib
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
assert os.path.isdir(os.path.join(REPO, "Assets")), REPO

A = lambda *p: os.path.join(REPO, "Assets", *p)
SCRIPT_DIR = A("_Scripts", "Controller", "Environment", "FloraAndFauna", "Builders")
CONFIG_DIR = A("_SO_Assets", "Builder Colonies")
CELL_DIR = A("_SO_Assets", "Cell Configs", "Swarm Cell")
PREFAB_DIR = A("_Prefabs", "FloraAndFauna")
SWARM_PREFAB = os.path.join(PREFAB_DIR, "SwarmTadpole.prefab")
MEMBER_SHADER = A("_Graphics", "Materials", "Graphs", "SwarmMemberInstanced.shader")
CONFIG_SO_CS = os.path.join(SCRIPT_DIR, "BuilderColonyConfigSO.cs")

SCRIPTS = ["BuilderCore", "BuilderColonyCore", "ThiefNestCore", "BuilderRegistry", "BuilderColonyConfigSO",
           "BuilderPrismWorld", "BuilderColonyFauna", "WearerCore"]
THEME = "d45a23e6bd2da304988606fba6c97628"            # Assets/_SO_Assets/ThemeManagerDataContainer.asset
RUNTIME_CELL_DATA = "8d4e8398eedc76c4dadb8604f89b9e1b"
FAUNA_SO_SCRIPT = "c778cfbe4dfc4c5c8401e40c17802311"  # FaunaConfigurationSO
TADPOLE_MB_FID = "5945480239701989318"                # SwarmTadpoleFauna on SwarmTadpole.prefab
ROOT_MB_FID = "6112640519920814101"
ROOT_GO_FID = "6112640519920814102"
ROOT_TR_FID = "6112640519920814103"
ELEMENT_ID = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}

# the swarm cell's geometry (author_swarm_fauna.py) - read for the band checks
NUCLEUS_RADIUS = 392.0
MEMBRANE_RADIUS = 1200.0
SWARM_BANDS = [(470, 620), (690, 840), (910, 1080)]

SPECIES = [
    dict(key="Fortress", species=0, prefab="BuilderFortressColony.prefab", config="FortressColonyConfig.asset",
         band=(845, 905), element="Mass", count=1,
         overrides={"MaxProxies": 24, "EngageRadius": 140}),
    dict(key="Thief Nest", species=1, prefab="BuilderThiefNest.prefab", config="ThiefNestConfig.asset",
         band=(1085, 1140), element="Space", count=1,
         # thieves fly at 150 u/s: the proxy ring is wider so a ship meets a real body, and no wider than the hoard raid
         # SpotRange 400 (round 11-14, Docs/SWARM_FAUNA.md §26.6): the research's 700 u sight, from a nest on the
         # 1085-1140 shell, put every pilot anywhere in a 1,200 u cell inside the nest's sight sphere, so the nest never
         # roosted; at ScoutRange it sees the ships whose wake it can reach
         overrides={"MaxProxies": 18, "EngageRadius": 200, "SpotRange": 400}),
    dict(key="Wearer", species=2, prefab="BuilderWearer.prefab", config="WearerConfig.asset",
         band=(400, 465), element="Charge", count=1,
         # 16 proxies (+32 colliders) keeps the engaged worst case at 1,192 / 1,200 with the threat grove; a creature's
         # worn prisms are the platform's own (0 new colliders) - Docs/BUILDERS_AND_THIEVES.md §10.4
         # WearSight 400 (round 11-14): 700 u from hearts in the 400-465 gap is the whole cell - the colony never roosted
         overrides={"MaxProxies": 16, "EngageRadius": 160, "WearSight": 400}),
]
# the member cap field of each species (its bodies' worst case in the phase ladder)
MEMBER_CAP = {0: "MaxWorkers", 1: "MaxThieves", 2: "MaxWearerHearts"}


def guid(name):
    return hashlib.md5(("cosmicshore-builders:" + name).encode()).hexdigest()


def swarm_guid(name):
    """author_swarm_fauna.py's guid scheme (the tadpole prefab and the member shader are its)."""
    return hashlib.md5(("cosmicshore-swarm-fauna:" + name).encode()).hexdigest()


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


def config_path(s):
    return os.path.join(CONFIG_DIR, s["config"])


def prefab_path(s):
    return os.path.join(PREFAB_DIR, s["prefab"])


def cell_name(s):
    return f"Swarm {s['key']} Builder Fauna Config Data"


def cell_path(s):
    return os.path.join(CELL_DIR, cell_name(s) + ".asset")


# ── the config: BuilderColonyConfigSO's C# defaults, as YAML ────────────────────────────────────

def _num(x):
    x = x.strip().rstrip("f").rstrip("F")
    v = float(x)
    return str(int(v)) if v == int(v) and "." not in x else ("%.6g" % v)


def cs_fields():
    """(name, yaml value) for every serialized field of BuilderColonyConfigSO, in declaration order, from its C#
    initialisers. Object references are filled by the caller."""
    src = read(CONFIG_SO_CS)
    body = src[src.index("public class BuilderColonyConfigSO"):]
    fields = []
    for m in re.finditer(r"^\s*(?:\[[^\]]*\]\s*)*public (\w+) (\w+)(?: = ([^;]+))?;", body, re.M):
        typ, name, init = m.group(1), m.group(2), (m.group(3) or "").strip()
        if typ in ("BuilderSpecies", "BuilderMendRule"):
            enum = {"BuilderSpecies": {"Fortress": 0, "Thieves": 1, "Wearers": 2},
                    "BuilderMendRule": {"None": 0, "Gap": 1, "Alarm": 2, "Both": 3}}[typ]
            fields.append((name, str(enum[init.split(".")[-1]])))
        elif typ == "bool":
            fields.append((name, "1" if init == "true" else "0"))
        elif typ in ("int", "float"):
            fields.append((name, _num(init)))
        elif typ in ("Vector3", "Vector4"):
            nums = [_num(x) for x in re.sub(r"new\s*(Vector\d)?\(|\)", "", init).split(",")]
            keys = "xyzw"[:len(nums)]
            fields.append((name, "{" + ", ".join(f"{k}: {v}" for k, v in zip(keys, nums)) + "}"))
        elif typ in ("SwarmTadpoleFauna", "Shader", "ThemeManagerDataContainerSO"):
            fields.append((name, None))
        else:
            raise SystemExit(f"BuilderColonyConfigSO.{name}: type {typ} has no YAML rule here - teach author_builders.py")
    return fields


def config_asset(s):
    refs = {
        "MemberPrefab": f"{{fileID: {TADPOLE_MB_FID}, guid: {swarm_guid(rel(SWARM_PREFAB))}, type: 3}}",
        "MemberShader": f"{{fileID: 4800000, guid: {swarm_guid(rel(MEMBER_SHADER))}, type: 3}}",
        "Theme": f"{{fileID: 11400000, guid: {THEME}, type: 2}}",
    }
    lines = []
    for name, value in cs_fields():
        if name == "Species":
            value = str(s["species"])
        elif name in s["overrides"]:
            value = str(s["overrides"][name])
        elif value is None:
            value = refs[name]
        lines.append(f"  {name}: {value}\n")
    return SO_HEADER % (script_guid("BuilderColonyConfigSO"), s["config"][:-6]) + "".join(lines)


def anchor_prefab(s):
    name = s["prefab"][:-7]
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
  m_Script: {{fileID: 11500000, guid: {script_guid('BuilderColonyFauna')}, type: 3}}
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
  config: {{fileID: 11400000, guid: {guid(rel(config_path(s)))}, type: 2}}
"""


def fauna_asset(s):
    lo, hi = s["band"]
    n = s["count"]
    return SO_HEADER % (FAUNA_SO_SCRIPT, cell_name(s)) + (
        f"  FaunaPrefab: {{fileID: {ROOT_MB_FID}, guid: {guid(rel(prefab_path(s)))}, type: 3}}\n"
        f"  InitialSpawnCount: {n}\n  PopulationSize: {n}\n  SpawnProbability: 1\n  NetworkSynced: 0\n"
        "  FeedsPerOffspring: 0\n  OffspringPerBirth: 1\n  ReproductionCooldownSeconds: 10\n"
        f"  MaxLivePopulation: {n}\n  ReleaseTier: 0\n"
        f"  BandInnerRadius: {lo}\n  BandOuterRadius: {hi}\n  CenterFocusBias: 0\n"
        f"  Element: {ELEMENT_ID[s['element']]}\n"
        "  Variant:\n    Enabled: 0\n"
        "  SpreadElements: 0\n  ElementPalette: []\n")


# ── what author_swarm_fauna.py reads ────────────────────────────────────────────────────────────

BUILDER_CELL_FILES = [cell_path(s) for s in SPECIES] + [cell_path(s) + ".meta" for s in SPECIES]


def profile_entries():
    """The cell spawn profile's SupportedFaunas lines for the builder species (author_swarm_fauna.profile_asset)."""
    return "".join(f"  - {{fileID: 11400000, guid: {guid(rel(cell_path(s)))}, type: 2}}\n" for s in SPECIES)


def body_volume():
    """The colonies' member BODIES for the phase ladder (author_swarm_fauna.ladder). A data-only member's body is a
    PrismSpatialIndex virtual entry bound with BindVirtualMass (SwarmEntryLedger: volume |x*y*z| of BodyScale), so the
    cell's LiveVolume counts it; a proxy's real body prism replaces it one for one. Worst case: every colony at its
    member cap. What they STEAL (walls, hoards, a wearer's worn body and lair) is the cell's own mass already counted -
    it changes hands, it is not added."""
    f = dict(cs_fields())
    total = 0.0
    for s in SPECIES:
        def val(k):
            return float(s["overrides"].get(k, f[k]))
        scale = [float(x.split(":")[1]) for x in f["BodyScale"].strip("{}").split(",")]
        total += s["count"] * val(MEMBER_CAP[s["species"]]) * abs(scale[0] * scale[1] * scale[2])
    return total


def proxy_colliders():
    """Worst case: every colony's proxies engaged at once, two colliders each (heart + body prism)."""
    return sum(2 * s["count"] * s["overrides"]["MaxProxies"] for s in SPECIES)


# ── assemble ────────────────────────────────────────────────────────────────────────────────────

def emit():
    out = {}
    for d in (SCRIPT_DIR, CONFIG_DIR):
        out[d + ".meta"] = FOLDER_META % guid(rel(d))
    for sc in SCRIPTS:
        out[os.path.join(SCRIPT_DIR, sc + ".cs.meta")] = SCRIPT_META % script_guid(sc)
    for s in SPECIES:
        for p, t, meta in ((config_path(s), config_asset(s), ASSET_META), (prefab_path(s), anchor_prefab(s), PREFAB_META),
                           (cell_path(s), fauna_asset(s), ASSET_META)):
            out[p] = t
            out[p + ".meta"] = meta % guid(rel(p))
    return out


def verify(out):
    problems = []
    minted = {re.search(r"^guid: (\w+)", t, re.M).group(1): p for p, t in out.items() if p.endswith(".meta")}
    for g, p in minted.items():
        owners = subprocess.run(["grep", "-rl", "--include=*.meta", f"^guid: {g}", A()],
                                capture_output=True, text=True).stdout.splitlines()
        owners = [o for o in owners if os.path.abspath(o) != os.path.abspath(p)]
        if owners:
            problems.append(f"guid {g} of {rel(p)} is already owned by {rel(owners[0])}")
    # every script in the folder has a meta this script owns (a new .cs with no meta never imports)
    for f in sorted(os.listdir(SCRIPT_DIR)):
        if f.endswith(".cs") and f[:-3] not in SCRIPTS:
            problems.append(f"{rel(os.path.join(SCRIPT_DIR, f))} is not in SCRIPTS (no owned .meta)")
    for s in SPECIES:
        lo, hi = s["band"]
        if lo <= NUCLEUS_RADIUS or hi >= MEMBRANE_RADIUS:
            problems.append(f"{s['key']}: band {s['band']} leaves the cytoplasm")
        for a, b in SWARM_BANDS:
            if lo < b and hi > a:
                problems.append(f"{s['key']}: band {s['band']} overlaps a swarm band {(a, b)} (round 11e: off the swarms' bands)")
        if s["element"] not in ELEMENT_ID:
            problems.append(f"{s['key']}: element {s['element']}")
    for a, b in zip(SPECIES, SPECIES[1:]):
        if a["band"][1] > b["band"][0] and b["band"][1] > a["band"][0]:
            problems.append(f"builder bands overlap: {a['key']} {a['band']} vs {b['key']} {b['band']}")
    # the asset carries every serialized field the C# declares, and nothing the C# no longer has
    names = [n for n, _ in cs_fields()]
    for s in SPECIES:
        text = out[config_path(s)]
        have = re.findall(r"(?m)^  (\w+): ", text.split("m_EditorClassIdentifier:\n", 1)[1])
        if have != names:
            problems.append(f"{s['config']}: fields {have} != BuilderColonyConfigSO's {names}")
        for k in s["overrides"]:
            if k not in names:
                problems.append(f"{s['key']}: override {k} is not a BuilderColonyConfigSO field")
    # the C# defaults the research quotes (Docs/BUILDERS_AND_THIEVES.md §1) - a changed default is a design change
    f = dict(cs_fields())
    for k, v in (("DefendCaste", "0.3"), ("Mend", "3"), ("LadenSpeed", "75"), ("ThiefSpeed", "150"), ("WarmSeconds", "1.5"),
                 ("SpotRange", "700"), ("ThiefFounders", "6"), ("MaxThieves", "18")):
        if f.get(k) != v:
            problems.append(f"BuilderColonyConfigSO.{k} defaults to {f.get(k)}, the research's value is {v}")
    if float(f["LadenSpeed"]) * 2 != float(f["ThiefSpeed"]):
        problems.append("a laden thief flies at HALF its free speed (the counterplay)")
    # own colour is the STARVATION fallback (Docs/BUILDERS_AND_THIEVES.md §2.1): desperate sits clearly below hungry, or
    # a merely hungry member would graze its own colour as readily as the opposing team's (the core clamps it there too)
    for who in ("Worker", "Thief", "Wearer"):
        for s in SPECIES:
            own = float(s["overrides"].get(f"{who}OwnDomainBelow", f[f"{who}OwnDomainBelow"]))
            hungry = float(s["overrides"].get(f"{who}HungryBelow", f[f"{who}HungryBelow"]))
            if not 0.0 < own < hungry:
                problems.append(f"{s['config']}: {who}OwnDomainBelow {own} must sit in (0, {who}HungryBelow {hungry}) - "
                                f"own colour is food only for a desperate member")
    for p in (SWARM_PREFAB, MEMBER_SHADER):
        if not os.path.exists(p + ".meta") or swarm_guid(rel(p)) not in read(p + ".meta"):
            problems.append(f"{rel(p)}: not the guid author_swarm_fauna.py mints (the member look is the swarm's)")
    return problems


def report():
    print("Builder colonies (round 11e) - in the Swarm cell, off the swarms' bands\n")
    for s in SPECIES:
        print(f"  {s['key']:<10} {s['band'][0]:>4}-{s['band'][1]:<4}u  x{s['count']}  hearts {s['element']:<6} "
              f"proxies <= {s['overrides']['MaxProxies']} within {s['overrides']['EngageRadius']} u of a vessel")
    print(f"  member bodies at every cap: {body_volume():,.1f} volume (BindVirtualMass - in author_swarm_fauna's ladder)")
    print(f"  colliders: 0 for every structure (stolen prisms keep their own); up to {proxy_colliders()} proxy colliders "
          f"only while vessels are near (counted in author_swarm_fauna.py's ceiling)")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()
    out = emit()
    report()
    problems = verify(out)
    if args.check:
        drift = [rel(p) for p, t in out.items() if not os.path.exists(p) or read(p) != t]
        if problems or drift:
            print("\nFAIL")
            for p in problems + [f"differs from what this script authors: {d}" for d in drift]:
                print("  - " + p)
            return 1
        print("\nOK - every builder asset matches the model.")
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
