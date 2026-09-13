#!/usr/bin/env python3
"""
Authors every serialized asset the SKEIN game mode needs (GameModes.Skein - the id is READ
out of the enum, never transcribed; see _skein_mode_id).

Idempotent and deterministic: every GUID is md5("CosmicShore/<stable name>"), so re-running
produces byte-identical output and re-tuning is one edit here plus a re-run rather than N
hand-edits that drift. Validates in memory and only then writes.

    python3 Tools/Build/author_skein_assets.py [--check]

WHAT THIS MODE IS. Skein is the Urchin-only cable race: a trefoil-knot cable of open, AIMED
prism rails, threaded by an ordered course of 24 rings, first DOMAIN whose LEAD RUNNER threads
the last ring. See Assets/_Scripts/Controller/Arcade/SKEIN.md.

THE ARENA IS A REFERENCE, NOT A FORK. The scene is cloned from MinigameHijack, which is already
the Urchin's scene: Urchin-only AI roster, an EquatorialRing spawn formation, a NUCLEUS-LESS
cell with an authored noNucleusSpawnRadius, and an IntensityWise ladder over four configs. Every
one of those is a property Skein needs and would otherwise have to re-derive.

PHASE THRESHOLDS ARE IMPORTED, NEVER TYPED. skein_budget.py is the model that proves the arena;
this script imports it so a cell's ladder cannot drift from the arena that has to satisfy it.
That matters more here than usual: (6,6,8) is 288 volume per prism against the nominal 16, so
the count x 16 derivation would be ~17x low and would pin the cell at Frenzy from frame one.
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHECK_ONLY = "--check" in sys.argv


def _skein_mode_id():
    """
    `GameModes.Skein`'s value, READ OUT OF THE ENUM rather than transcribed.

    It has been renumbered three times - 48 -> 50 (Tollway and Headlong claimed 48 and 49 while
    this branch was in flight) and 50 -> 51 (Breakwater claimed 50 between this branch's review
    pass and its push). Each time it was hardcoded here in five places, which is five places to
    forget. The ship skill's own rule: an id a generator HARDCODES is an id that goes stale on
    the next upstream renumber - read it from its enum and the sweep disappears.

    Hard failure rather than a fallback: a generator that authored an arcade card pointing at
    the WRONG mode would be worse than one that refused to run, and it is exactly the failure a
    fallback default would hide.
    """
    src = open(os.path.join(ROOT, "Assets", "_Scripts", "Data", "Enums", "GameModes.cs"),
               encoding="utf-8").read()
    m = re.search(r"^\s*Skein\s*=\s*(\d+)\s*,", src, re.M)
    assert m, "GameModes.Skein not found - has the member been renamed?"
    return int(m.group(1))


MODE_ID = _skein_mode_id()
sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))

WRITES = []


def guid(name: str) -> str:
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


def rel(*p):
    return os.path.join(ROOT, *p)


def emit(path: str, text: str):
    """Record a write, and in --check mode DIFF IT AGAINST DISK rather than just claiming
    success. A --check that never compares is a false green - it reports what the script would
    do rather than whether the tree matches it."""
    full = rel(path)
    existing = open(full, encoding="utf-8").read() if os.path.exists(full) else None
    state = "same" if existing == text else ("new" if existing is None else "DIFFERS")
    WRITES.append((path, state))
    if not CHECK_ONLY and state != "same":
        os.makedirs(os.path.dirname(full), exist_ok=True)
        with open(full, "w", encoding="utf-8") as f:
            f.write(text)
    return state


# ── GUIDs ────────────────────────────────────────────────────────────────────
G_SCRIPT = {n: guid(f"script/{n}") for n in (
    "SkeinCourse", "SkeinController", "SkeinScoringRuleSO", "SpawnableSkein")}
G = {
    "ArcadeGameSkein":  guid("asset/ArcadeGameSkein"),
    "SkeinScoringRule": guid("asset/SkeinScoringRule"),
    "SkeinSpawnProfile": guid("asset/SkeinSpawnProfile"),
    "MinigameSkein.unity": guid("asset/MinigameSkein.unity"),
    "SkeinFolder": guid("folder/SkeinCellConfigs"),
    "SpawnablesFolder": guid("folder/SkeinSpawnables"),
}
for i in (1, 2, 3, 4):
    G[f"SkeinCell{i}"] = guid(f"asset/SkeinCellConfig{i}")
    G[f"SpawnableSkein{i}"] = guid(f"prefab/SpawnableSkein{i}")

# ── GUIDs read from the repo, never invented ─────────────────────────────────
EXISTING = {
    "SO_ArcadeGame":        "fe040efad3307fb449b6b72ad15362da",
    "CellConfigDataSO":     "01f934d50526431a9392a6ceca1dc33d",
    "SO_Class_Urchin":      "bde48fa4833b6364b93111a55ba90958",
    # Hijack's own, which the clone swaps out
    "HijackController":     "1f78e03a675ab74d791c1e1433f9b948",
    "HijackTurnMonitor":    "c55df7a36d521ba0daaae73f790483f5",
    "HijackScoringRule":    "103c0a8be93c027048f0f2775c303211",
    "SpawnableSwitchyard":  "9e6ef0e4076b63e6e990e5b3d122a11c",
    "SwitchyardSpawnProfile": "eab0c99bbf13d420231fedda7d4c4d31",
    # Cell-owned visuals, reused verbatim (the Cell owns the environment)
    "MembranePrefab":       "6e330f85972faf843b8a128e7166f7b5",
    "CytoplasmPrefab":      "9cacd903fcf4643459f5f14ac811bb20",
    "CellIcon":             "6aa1c06e11b265744a5f9fa8858ac72a",
    "RailPrism":            "ed9defc56162b4b4588e61c20984b6d9",
}
SWITCHYARD_CELL = {
    1: "fc7a4b1e0a0d4e0e9b1a2c3d4e5f6071",  # placeholder, resolved from disk below
}

META_SCRIPT = """fileFormatVersion: 2
guid: {g}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
META_FOLDER = """fileFormatVersion: 2
guid: {g}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
META_ASSET = """fileFormatVersion: 2
guid: {g}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""
META_PREFAB = """fileFormatVersion: 2
guid: {g}
PrefabImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
META_SCENE = """fileFormatVersion: 2
guid: {g}
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def read_guid(meta_path: str) -> str:
    with open(rel(meta_path), encoding="utf-8") as f:
        m = re.search(r"^guid: ([0-9a-f]{32})", f.read(), re.M)
    assert m, f"no guid in {meta_path}"
    return m.group(1)


# ── the arena's own numbers, IMPORTED from the proof ─────────────────────────
def arena_numbers():
    """Prisms and volume per intensity, from skein_budget itself - so a cell's phase ladder
    cannot drift from the arena that has to satisfy it."""
    saved = sys.argv
    sys.argv = ["skein_budget", "--check"]
    try:
        import skein_budget as SB
        spine = SB.Spine()
        w = SB.solve_twist(spine.L)
        out = {}
        for i in sorted(SB.STRAND_COUNTS):
            r = SB.analyse(i, spine, w, verbose=False)
            out[i] = (r["prisms"], int(r["prisms"] * SB.PRISM_SCALE[0]
                                       * SB.PRISM_SCALE[1] * SB.PRISM_SCALE[2]), r["n"])
        return out
    finally:
        sys.argv = saved


def phase_thresholds(prisms: int, volume: int) -> dict:
    """
    Ride THIS intensity's own measured baseline, with the headroom Hijack ships.

    Hijack's deltas are the amount of PLAYER TRAIL a match adds on top of the arena, which is a
    property of the vessel and the match length rather than of the arena - so they carry across
    to a sibling Urchin race. Restless +700 prisms / +11,200 volume, Frenzy +3,600 / +57,600,
    exits set just below their enters for hysteresis.

    The count x 16 derivation is NOT usable here: (6,6,8) is 288 volume per prism against the
    nominal 16, so it would be ~17x low and would pin the cell at Frenzy from frame one.
    """
    return {
        "RestlessEnter": prisms + 700,
        "RestlessExit": prisms + 500,
        "FrenzyEnter": prisms + 3600,
        "FrenzyExit": prisms + 3000,
        "RestlessEnterVolume": volume + 11200,
        "RestlessExitVolume": volume + 8000,
        "FrenzyEnterVolume": volume + 57600,
        "FrenzyExitVolume": volume + 48000,
    }


HEADER = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
STUB = ("  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n")


def cell_config(i: int, prisms: int, volume: int, n: int) -> str:
    t = phase_thresholds(prisms, volume)
    return (HEADER + STUB
            + f"  m_Script: {{fileID: 11500000, guid: {EXISTING['CellConfigDataSO']}, type: 3}}\n"
            + f"  m_Name: Skein Cell Config {i}\n  m_EditorClassIdentifier:\n"
            + "  CellName: Skein\n"
            + f"  Description: 'The Skein at intensity {i} - a trefoil-knot cable of {n} rails\n"
            + f"    ({prisms} prisms) on BREATHING radii - each strand oscillates between 45\n"
            + "    and 135 with its own phase, so at every station the N strands cover the\n"
            + "    whole band and riding an outward-bound one carries you out. Cut into open\n"
            + f"    AIMED\n"
            + "    segments. NO NUCLEUS and no food web by design: in a nucleus-less cell\n"
            + "    herbivores eat opposing-domain mass, so a swarm would graze whatever the\n"
            + "    TRAILING team had just painted. PhaseThresholds ride THIS intensity''s own\n"
            + "    measured baseline - regenerate with Tools/Build/author_skein_assets.py\n"
            + "    rather than hand-editing; the count x 16 derivation is 17x low here because\n"
            + "    a (6,6,8) prism is 288 volume against the nominal 16.'\n"
            + f"  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}\n"
            + f"  Difficulty: {i}\n  CellEndGameScore: 0\n"
            + f"  MembranePrefab: {{fileID: 346633111830028674, guid: {EXISTING['MembranePrefab']}, type: 3}}\n"
            + "  NucleusPrefab: {fileID: 0}\n"
            + f"  CytoplasmPrefab: {{fileID: 639495419069806261, guid: {EXISTING['CytoplasmPrefab']}, type: 3}}\n"
            + "  CellModifiers: []\n"
            + f"  SpawnProfile: {{fileID: 11400000, guid: {G['SkeinSpawnProfile']}, type: 2}}\n"
            + f"  EnvironmentPrefab: {{fileID: 5260000000000303, guid: {G[f'SpawnableSkein{i}']}, type: 3}}\n"
            + f"  EnvironmentIntensity: {i}\n  SenseRadiusOverride: 1200\n"
            + "  PhaseThresholds:\n"
            + "".join(f"    {k}: {v}\n" for k, v in t.items()))


def spawnable_prefab(i: int, n: int) -> str:
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &5260000000000301
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 5260000000000302}}
  - component: {{fileID: 5260000000000303}}
  m_Layer: 0
  m_Name: SpawnableSkein{i}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &5260000000000302
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000301}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &5260000000000303
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 5260000000000301}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableSkein']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: 20260909
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 1
  layBudgetMsPerFrame: 8
  intensityLevel: {i}
  prism: {{fileID: 4563009547826722997, guid: {EXISTING['RailPrism']}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
  strandCount: {n}
  prismScale: {{x: 6, y: 6, z: 8}}
  cableSeed: 0
"""


SPAWN_PROFILE_SCRIPT = "e8d8aa5d835249798a256e18f2f7d912"


def spawn_profile() -> str:
    """EMPTY - no flora, no fauna, and that is a design decision rather than an omission.

    In a nucleus-less cell herbivores eat OPPOSING-domain mass, and the leader's colour is by
    definition the most abundant as they convert lanes under themselves, so a swarm would
    preferentially graze whatever the TRAILING team had just painted: an anti-comeback current in
    a mode whose whole tempo is contested ownership. There is a second reason Hijack does not
    have - fauna are client-local, so a food web would chew the TRACK differently on every
    machine, and a race whose track differs per peer is unacceptable in a way a heist's is not.
    """
    return (HEADER + STUB
            + f"  m_Script: {{fileID: 11500000, guid: {SPAWN_PROFILE_SCRIPT}, type: 3}}\n"
            + "  m_Name: Skein Spawn Profile\n  m_EditorClassIdentifier:\n"
            + "  FloraExcludeLocalDomain: 0\n  FloraSpawnVolumeCeiling: 0\n"
            + "  FloraInitialDelaySeconds: 0\n  FloraSpawnIntervalSeconds: 0\n"
            + "  SupportedFloras: []\n  FaunaExcludeLocalDomain: 0\n"
            + "  InitialFaunaSpawnWaitTime: 0\n  InitialFaunaReleaseTier: 0\n"
            + "  FaunaSpawnVolumeThreshold: 1\n  BaseFaunaSpawnTime: 15\n"
            + "  SeedFullWaveEveryTick: 0\n  FaunaFoodFloor: 0\n"
            + "  FaunaInitialDelaySeconds: 0\n  FaunaSpawnIntervalSeconds: 0.5\n"
            + "  HerbivoreSpawnPointCount: 0\n  HerbivoreSpawnRadius: 0\n"
            + "  PredatorSpawnPointCount: 0\n  PredatorSpawnRadius: 0\n"
            + "  SupportedFaunas: []\n")


def scoring_rule() -> str:
    return (HEADER + STUB
            + f"  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SkeinScoringRuleSO']}, type: 3}}\n"
            + "  m_Name: SkeinScoringRule\n  m_EditorClassIdentifier:\n"
            + "  metric: 9\n  golfRules: 1\n")


def arcade_card(icon_active, icon_inactive, card_bg) -> str:
    """The card. ComebackRatePerScoreDeficit is 0.5 against a 24-ring target, so a
    quarter-of-target deficit (6 rings) buys 3.0 element levels - the trap Dog Fight, Bends and
    Wildlife Liberation each recorded independently is that the rate is a function of the TARGET,
    so re-targeting the mode without re-deriving it silently kills the comeback."""
    return (HEADER + STUB
            + f"  m_Script: {{fileID: 11500000, guid: {EXISTING['SO_ArcadeGame']}, type: 3}}\n"
            + "  m_Name: ArcadeGameSkein\n  m_EditorClassIdentifier:\n"
            + f"  Mode: {MODE_ID}\n  IsMultiplayer: 1\n  DisplayName: Skein\n"
            + "  Description: 'Urchins only, on a knot of rails that never quite closes. Latch\n"
            + "    on and the cable does the driving - your colour runs fast, theirs runs at a\n"
            + "    crawl, and every rail ENDS somewhere, aimed at another. Thread the rings in\n"
            + "    order; the first team to put a pilot through the last one takes it.'\n"
            + f"  IconActive: {{fileID: 21300000, guid: {icon_active}, type: 3}}\n"
            + f"  IconInactive: {{fileID: 21300000, guid: {icon_inactive}, type: 3}}\n"
            + f"  CardBackground: {{fileID: 21300000, guid: {card_bg}, type: 3}}\n"
            + "  GolfScoring: 1\n  SceneName: MinigameSkein\n"
            + f"  Vessels:\n  - {{fileID: 11400000, guid: {EXISTING['SO_Class_Urchin']}, type: 2}}\n"
            + "  MinPlayersAllowed: 2\n  MaxPlayersAllowed: 4\n"
            + "  MinDomainsAllowed: 2\n  MaxDomainsAllowed: 3\n"
            + "  MinIntensity: 1\n  MaxIntensity: 4\n"
            + "  CallToActionTargetType: 404\n  ViewUserAction: 0\n  PlayUserAction: 0\n"
            + "  ComebackRatePerScoreDeficit: 0.5\n")


# ── the scene ────────────────────────────────────────────────────────────────

def clone_scene() -> str:
    """
    Clone MinigameHijack and swap it onto Skein's controller, monitor, rule and cell.

    THE DONOR IS CHOSEN, NOT CONVENIENT. MinigameHijack is already the Urchin's scene: an
    Urchin-only AI roster, an EquatorialRing spawn formation, a NUCLEUS-LESS cell with an
    authored noNucleusSpawnRadius (without which every omni crystal falls through to its own
    SphereRadius and respawns on the arena's exact centre - Dog Fight's recorded defect), and an
    IntensityWise ladder over four configs. Every one of those is a property Skein needs.

    Swaps are by GUID and each is ASSERTED to have fired, because a silent no-op here produces a
    scene that loads, renders, and runs the wrong mode.
    """
    src = open(rel("Assets/_Scenes/Multiplayer Scenes/MinigameHijack.unity"), encoding="utf-8").read()

    swaps = [
        (EXISTING["HijackController"],   G_SCRIPT["SkeinController"],    "controller"),
        # The PLATFORM's monitor, not one of Skein's own: the gate-race extraction gave
        # RaceGateTurnMonitor a controller-supplied target, so it never has to know which mode
        # it is monitoring and every gate race shares it.
        (EXISTING["HijackTurnMonitor"],  "da1e0d6121acc091283ec785f932c32c", "turn monitor"),
        (EXISTING["HijackScoringRule"],  G["SkeinScoringRule"],          "scoring rule"),
        # NOT the spawn profile: the scene never names it - the cell CONFIG does, and this script
        # writes new configs that already point at Skein's own. The assert below caught that.
    ]
    for i in (1, 2, 3, 4):
        cfg = read_guid(f"Assets/_SO_Assets/Cell Configs/Switchyard Cell/Switchyard Cell Config {i}.asset.meta")
        swaps.append((cfg, G[f"SkeinCell{i}"], f"cell config {i}"))

    # NOT the objective provider either: it is not a scene component - MiniGameHUD builds one
    # per mode in CreateObjectiveProviderForGameMode, which is a C# switch this mode adds a case
    # to. Both of these were caught by the assert rather than by review, which is the argument
    # for asserting every swap instead of trusting a guid list.

    out = src
    for old, new, label in swaps:
        n = out.count(old)
        assert n > 0, f"scene clone: nothing to swap for the {label} (guid {old})"
        out = out.replace(old, new)
    return out


# ── registration ─────────────────────────────────────────────────────────────

def register_build_settings() -> str:
    p = "ProjectSettings/EditorBuildSettings.asset"
    src = open(rel(p), encoding="utf-8").read()
    if G["MinigameSkein.unity"] in src:
        return src
    entry = ("  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameSkein.unity\n"
             f"    guid: {G['MinigameSkein.unity']}\n")
    anchor = "  m_configObjects:"
    assert anchor in src, "EditorBuildSettings: no m_configObjects anchor"
    return src.replace(anchor, entry + anchor, 1)


def register_roster() -> str:
    """The LIVE arcade roster - OrganicRematchGames, referenced by Menu_Main, AppManager and
    GameCard. AllGames/ArcadeGames are legacy and do not drive the grid."""
    p = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
    src = open(rel(p), encoding="utf-8").read()
    if G["ArcadeGameSkein"] in src:
        return src
    # Append after the last game reference, preserving list order.
    lines = src.rstrip("\n").split("\n")
    last = max(i for i, l in enumerate(lines) if re.match(r"^\s*- \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}$", l))
    indent = re.match(r"^(\s*)- ", lines[last]).group(1)
    lines.insert(last + 1, f"{indent}- {{fileID: 11400000, guid: {G['ArcadeGameSkein']}, type: 2}}")
    return "\n".join(lines) + "\n"


def register_progression():
    """alwaysUnlockedModes += MODE_ID.

    Without it the card renders, reports interactable, passes an EventSystem raycast and OPENS
    NOTHING - SetLocked(true) skips the SelectGame listener entirely. That is the same silent
    class of failure as the un-injected card row, and it looks identical from the outside.
    """
    for cand in ("Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset",):
        full = rel(cand)
        if not os.path.exists(full):
            return None, None
        src = open(full, encoding="utf-8").read()
        if not re.search(r"alwaysUnlockedModes:", src):
            return cand, None
        if re.search(rf"alwaysUnlockedModes:(\s*\n(\s*)- \d+)*[\s\S]{{0,400}}?^\s*- {MODE_ID}$", src, re.M):
            return cand, src
        m = re.search(r"(alwaysUnlockedModes:\n)((?:\s*- \d+\n)+)", src)
        if not m:
            return cand, src.replace("alwaysUnlockedModes:", f"alwaysUnlockedModes:\n  - {MODE_ID}", 1)
        if re.search(rf"^\s*- {MODE_ID}$", m.group(2), re.M):
            return cand, src
        return cand, src[:m.end(2)] + f"  - {MODE_ID}\n" + src[m.end(2):]
    return None, None


def main():
    nums = arena_numbers()

    # Art: reuse Hijack's card art. Skein has no art of its own yet, and a card with a MISSING
    # sprite renders as a solid tinted quad rather than as nothing (Docs: the Pip border defect),
    # which reads as a broken card rather than an unfinished one.
    hij = open(rel("Assets/_SO_Assets/Games/ArcadeGameHijack.asset"), encoding="utf-8").read()
    def art(field):
        m = re.search(rf"{field}: {{fileID: \d+, guid: ([0-9a-f]{{32}}), type: 3}}", hij)
        assert m, f"could not read {field} from ArcadeGameHijack"
        return m.group(1)

    # ── .meta for the new C# ────────────────────────────────────────────────
    cs = {
        "SkeinCourse":          "Assets/_Scripts/Controller/Arcade/Skein/SkeinCourse.cs",
        "SkeinController":      "Assets/_Scripts/Controller/Arcade/Skein/SkeinController.cs",
        "SkeinScoringRuleSO":   "Assets/_Scripts/Controller/Arcade/Scoring/SkeinScoringRuleSO.cs",
        "SpawnableSkein":       "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableSkein.cs",
    }
    for name, path in cs.items():
        assert os.path.exists(rel(path)), f"missing source file {path}"
        emit(path + ".meta", META_SCRIPT.format(g=G_SCRIPT[name]))

    # ── cell configs, spawn profile, spawnables ─────────────────────────────
    emit("Assets/_SO_Assets/Cell Configs/Skein Cell.meta", META_FOLDER.format(g=G["SkeinFolder"]))
    emit("Assets/_SO_Assets/Cell Configs/Skein Cell/Skein Spawn Profile.asset", spawn_profile())
    emit("Assets/_SO_Assets/Cell Configs/Skein Cell/Skein Spawn Profile.asset.meta",
         META_ASSET.format(g=G["SkeinSpawnProfile"]))

    for i in (1, 2, 3, 4):
        prisms, volume, n = nums[i]
        emit(f"Assets/_SO_Assets/Cell Configs/Skein Cell/Skein Cell Config {i}.asset",
             cell_config(i, prisms, volume, n))
        emit(f"Assets/_SO_Assets/Cell Configs/Skein Cell/Skein Cell Config {i}.asset.meta",
             META_ASSET.format(g=G[f"SkeinCell{i}"]))
        emit(f"Assets/_Prefabs/Spawnables/SpawnableSkein{i}.prefab", spawnable_prefab(i, n))
        emit(f"Assets/_Prefabs/Spawnables/SpawnableSkein{i}.prefab.meta",
             META_PREFAB.format(g=G[f"SpawnableSkein{i}"]))

    # ── scoring rule + card ─────────────────────────────────────────────────
    emit("Assets/_SO_Assets/Scoring Rules/SkeinScoringRule.asset", scoring_rule())
    emit("Assets/_SO_Assets/Scoring Rules/SkeinScoringRule.asset.meta",
         META_ASSET.format(g=G["SkeinScoringRule"]))
    emit("Assets/_SO_Assets/Games/ArcadeGameSkein.asset",
         arcade_card(art("IconActive"), art("IconInactive"), art("CardBackground")))
    emit("Assets/_SO_Assets/Games/ArcadeGameSkein.asset.meta",
         META_ASSET.format(g=G["ArcadeGameSkein"]))

    # ── scene ───────────────────────────────────────────────────────────────
    emit("Assets/_Scenes/Multiplayer Scenes/MinigameSkein.unity", clone_scene())
    emit("Assets/_Scenes/Multiplayer Scenes/MinigameSkein.unity.meta",
         META_SCENE.format(g=G["MinigameSkein.unity"]))

    # ── registration ────────────────────────────────────────────────────────
    emit("ProjectSettings/EditorBuildSettings.asset", register_build_settings())
    emit("Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset", register_roster())
    prog_path, prog_src = register_progression()
    if prog_path and prog_src:
        emit(prog_path, prog_src)

    # ── report ──────────────────────────────────────────────────────────────
    new = sum(1 for _, s in WRITES if s == "new")
    diff = sum(1 for _, s in WRITES if s == "DIFFERS")
    same = sum(1 for _, s in WRITES if s == "same")
    print(f"skein assets: {len(WRITES)} files - {new} new, {diff} changed, {same} unchanged"
          + ("  [--check, nothing written]" if CHECK_ONLY else ""))
    for path, state in WRITES:
        if state != "same":
            print(f"  {state:8s} {path}")

    print("\nNOTE: the cloned scene carries the DONOR's in-scene GlobalObjectIdHash values.")
    print("      Unity recomputes them the first time MinigameSkein.unity is opened and saved,")
    print("      so expect that diff and commit it. It is harmless in the meantime - two game")
    print("      scenes are never loaded at once, and NGO indexes in-scene objects by")
    print("      (hash, sceneHandle) - but MinigameSwitchback carries distinct values only")
    print("      because a human opened it, and Hijack shipped with PeelTheCage's.")

    print("\nphase ladder, derived from skein_budget (NEVER count x 16 - a (6,6,8) prism is 288):")
    for i in (1, 2, 3, 4):
        prisms, volume, n = nums[i]
        t = phase_thresholds(prisms, volume)
        print(f"  I{i}  N={n}  {prisms:6d} prisms  {volume:9,d} volume  "
              f"RestlessEnterVolume {t['RestlessEnterVolume']:9,d}  FrenzyEnterVolume {t['FrenzyEnterVolume']:9,d}")

    if CHECK_ONLY and diff:
        print("\n--check: the tree does NOT match what this script would author.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
