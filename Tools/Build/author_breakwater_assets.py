#!/usr/bin/env python3
"""
Authors every serialized asset the BREAKWATER game mode needs (GameModes.Breakwater = 48).

Breakwater is the Sparrow-only STATION race. Fourteen ordered stations hang on a walk through the
cell; each is a shallow dish of plates opening back toward the pilot with its throat welded shut by
a triple-rake weave of danger bars around an 18-unit eye. You arrive with two rockets in the bay and
make one choice - FIRE (the skyburst's spherical blast vaporises a door), SAW (turret stance, stop
dead, grind the weave open) or THREAD (fly the eye at 1.46x hull clearance). What actually SCORES is
the switch ring at the port radius, so the three verbs are three PRICES for one crossing. Ammunition
is the arena itself (50 hostile prisms buy a rocket), so opening one door roughly funds the next.
First DOMAIN whose LEAD RUNNER threads station 14 wins.

    python3 Tools/Build/author_breakwater_assets.py [--check]

Every GUID is md5("CosmicShore/<stable name>"), so a re-run is byte-identical and a retune is one
edit here rather than N hand-edits that drift. Everything is validated IN MEMORY and only then
written.

WHAT THIS AUTHORS

  - .cs.meta for the seven new Breakwater scripts, plus the Breakwater/ folder's own .meta
  - the scoring rule: a SECOND ASSET on the EXISTING GateRaceScoringRuleSO. A station is a switch
    threaded in order - the same fact - so metric 9 (SwitchesThreaded), BestByDomain, the goal-stack
    row and the comeback source are all reused verbatim. Zero new scoring code.
  - four CellConfigDataSO on an IntensityWise ladder, with PhaseThresholds IMPORTED from
    Tools/Build/breakwater_arena.py rather than retyped
  - one SpawnProfileSO: no flora, no fauna
  - one SpawnableBreakwater prefab (see "ONE PREFAB, NOT FOUR" below)
  - the arcade card, the scene, and the four registrations

THE NUMBERS COME FROM THE MODEL, NEVER FROM THIS FILE. `breakwater_arena.py` is a bit-exact mirror
of BreakwaterStationBuilder + BreakwaterCourse - it reproduces the station geometry in closed form
and the walk's xorshift32 exactly - so it can price the arena rather than estimate it. This script
IMPORTS it. Retyping one of its numbers here would create a second place for it to live, and the
PhaseThresholds would then describe an arena nobody flies.

ONE PREFAB, NOT FOUR. Every intensity ships the SAME SpawnableBreakwater. Station geometry is a
closed-form function of the per-station PortRadius the CONTROLLER supplies from
BreakwaterCourseSettings.ForIntensity, and the prefab's only serialized fields are shoal tuning,
which the model prices identically at all four levels (13 legs x 6 x 7 = 546 prisms, 34,944 volume,
constant). Four variants would be four byte-identical assets differing in nothing - four places for
one number to drift, which is the exact failure the SpawnProfile trap below describes. Asserted:
the model's shoal totals are checked equal across all four intensities before this is believed.

THREE SAFETY RULES THIS GENERATOR HOLDS AND ITS PREDECESSORS DO NOT

(a) A SPENT ONE-SHOT STANDS DOWN, IT NEVER ABORTS. Every mode generator in this repo fails --check
    today. Three of the four fail for the same structural reason: a migration `assert` sitting ABOVE
    the validation section fires once the donor moves on and takes every check below it with it, so
    a green --check was never available to copy. Measured at the merge base:

        author_switchback_assets.py --check   2 files drifted (the card + the scene)
        author_drumfire_assets.py  --check    AssertionError: donor crystal block not found
        author_salvo_assets.py     --check    CallToActionTargetType is not a field on SO_ArcadeGame
        author_hijack_assets.py    --check    same

    Here the scene clone is a BOOTSTRAP, fenced by SCENE_ALREADY_SHIPPED. Nothing in this file
    raises; every failure appends to `errors` and the run reports all of them at once.

(b) RE-RUNNING A SCENE-CLONE GENERATOR IS DESTRUCTIVE. A measured re-run of
    author_switchback_assets.py rewrote MinigameSwitchback.unity and moved an in-scene
    NetworkObject's GlobalObjectIdHash (2537121143 -> 99744438) - a silent cross-peer scene-sync
    break with nothing in the console. Unity re-derives that hash when it saves a scene, so the
    file on disk is Unity's, not this script's, from the moment it ships. So: the clone runs ONCE,
    and every later run registers the shipped scene READ-ONLY and validates its wiring instead of
    rewriting it. The consequence is stated plainly - for the scene, --check's byte-diff is
    vacuous by construction and the WIRING assertions are what bite. That is the right trade: a
    diff that can only ever pass is worth less than a check that reads the shipped file and says
    whether the mode is still wired.

(c) --check MUST ACTUALLY PASS, AND MUST BE SEEN TO FAIL. Every generated file is diffed against
    disk with CRLF normalisation. The gate was proven by flipping a constant and watching it fail;
    see the branch's PR body.

RETIRED KEYS. `CallToActionTargetType` and `PreviewClip` are NOT fields on SO_ArcadeGame or SO_Game
and are not emitted. The key validator below derives its allowed set ENTIRELY from the C# - the
older generators carried a hand-written SO_BASE whitelist that listed `PreviewClip`, which is
precisely how a retired key survived a key check for months.

WHAT THIS DELIBERATELY DOES NOT TOUCH. `Assets/Resources/ObjectiveIconSet.asset` and
`Assets/Resources/ModeControlsLibrary.asset` already carry a metric-9 row (icon
eae5dbed618cd04cc66a6089b7c2d10d, label "Thread switches"), authored by author_switchback_assets.py.
Those tables are keyed on the METRIC, never on the mode, so Breakwater inherits the goal-stack row
and the launch-panel objective icon for free - and two generators SETting one row would flip the
file on every alternate run. `Assets/_Scripts/Controller/Arcade/Racing.meta` and
`RaceGateRing.cs.meta` are likewise owned by that generator and committed; emitting them here with a
guid re-derived from this mode's name would dangle every reference to the ring.
"""
import hashlib
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HERE = os.path.dirname(os.path.abspath(__file__))
CHECK_ONLY = "--check" in sys.argv

sys.path.insert(0, HERE)
import breakwater_arena as arena          # noqa: E402  - the measured model IS the authority

# Nothing in this file raises. Every problem lands here and the run reports all of them together,
# so one stale anchor can never hide the twelve checks underneath it (safety rule (a)).
errors: "list[str]" = []
notes: "list[str]" = []


def require(condition, message) -> bool:
    if not condition:
        errors.append(message)
    return bool(condition)


def guid(name: str) -> str:
    """Deterministic GUID for a stable asset name (asset-surgery: generator-authored family)."""
    return hashlib.md5(f"CosmicShore/{name}".encode()).hexdigest()


files: "dict[str, str]" = {}

# Every prose block yaml_text folds, kept so the round-trip below can PROVE it re-reads as the text
# that went in rather than trusting three hand-written plain-scalar rules.
folded_blocks: "list[tuple[str, str, str]]" = []


def emit(rel: str, content: str):
    files[rel] = content


def read(rel: str) -> str:
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


def exists(rel: str) -> bool:
    return os.path.exists(os.path.join(ROOT, rel))


def enum_value(rel: str, enum_name: str, member: str, default: int = -1) -> int:
    """Read a C# enum member's explicit value from source.

    Every id this script writes into an asset (a mode, a metric, a comeback source) is a NUMBER that
    travels while the code around it switches on the NAME - so a renumber upstream leaves the asset
    pointing at a different mode and nothing complains. Deriving it here means the enum is the
    single source and a rename fails loudly instead of a renumber failing silently.

    Drumfire's version of this raises SystemExit. Ours records an error and returns a sentinel,
    because aborting on the first missing member is the same shape as the spent-assert trap in
    safety rule (a): it would stop the twelve checks below from ever running.
    """
    src = read(rel)
    if ("enum " + enum_name) not in src:
        errors.append(f"{rel}: no enum {enum_name}")
        return default
    body = src[src.index("enum " + enum_name):]
    m = re.search(rf"^\s*{re.escape(member)}\s*=\s*(\d+)\s*,", body, re.M)
    if not m:
        errors.append(f"{rel}: enum {enum_name} has no explicitly-valued member {member!r} - "
                      f"this script cannot author an asset that points at it")
        return default
    return int(m.group(1))


def cs_const(rel: str, name: str, default=None):
    """A numeric literal out of C#. Matches both `const float X = 12f;` and an expression-bodied
    `protected override int X => 6000;` - the second form is how a base-class budget is overridden,
    and a reader that only knew `=` would report a real constant as missing. Used to prove the
    shipped code and the model agree, which is the whole basis for the PhaseThresholds being exact
    rather than approximate."""
    src = read(rel)
    m = re.search(rf"\b{re.escape(name)}\s*=>?\s*(-?[0-9]*\.?[0-9]+)f?\s*;", src)
    if not m:
        errors.append(f"{rel}: no numeric constant {name}")
        return default
    return float(m.group(1))


def cs_float_array(rel: str, expr: str):
    """`<expr> = new[] { 300f, 300f, 290f, 275f }[i - 1]` -> (300.0, 300.0, 290.0, 275.0). `expr`
    is whatever is to the LEFT of the array in the shipped source - a settings field for most rows,
    a local for MinStep (which is computed once and then assigned), and `return` for the port
    ladder, which lives in its own method."""
    src = read(rel)
    m = re.search(rf"{re.escape(expr)}\s*=?>?\s*new\[\]\s*\{{([^}}]*)\}}", src)
    if not m:
        errors.append(f"{rel}: no per-intensity array for {expr}")
        return None
    return tuple(float(x.strip().rstrip("f")) for x in m.group(1).split(",") if x.strip())


# ── Ids, all derived from the C# so a renumber upstream cannot go unnoticed ──────────────────
MODE_BREAKWATER = enum_value("Assets/_Scripts/Data/Enums/GameModes.cs", "GameModes", "Breakwater")
METRIC_SWITCHES_THREADED = enum_value("Assets/_Scripts/Data/Enums/ScoringMetric.cs",
                                      "ScoringMetric", "SwitchesThreaded")
COMEBACK_SWITCHES_THREADED = enum_value(
    "Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs",
    "ScoreDifferenceSource", "SwitchesThreaded")

# ── Tuning, IMPORTED rather than retyped ─────────────────────────────────────────────────────
#
# STATION_TARGET is read three ways and all three must agree: the model prices 14 stations of mass,
# EndConditionOverridesSO.DefaultBreakwaterStationTarget is what RaceGateTurnMonitor
# publishes as the target AND what BreakwaterController lays, and this script writes the override
# the tool window edits. Asserted below.
STATION_TARGET = arena.STATION_COUNT

# LAPS, and the RACE target they derive. The course is flown out and back, so the two numbers the
# old single `breakwaterStationTarget` conflated are now separate: STATION_TARGET is what the
# controller LAYS (and what the model prices as arena mass), CROSSING_TARGET is what a pilot
# THREADS and what the goal row counts to. See BreakwaterCourseSettings.DefaultLaps for why the
# course does not close into a circuit.
LAPS = arena.LAPS
CROSSING_TARGET = arena.crossing_target()

# The comeback strength, and it is a FUNCTION OF THE TARGET - `bonusLevels = deficit x rate` - so a
# rate only means anything beside the scale of deficits the mode produces. This is the trap
# DOGFIGHT.md, BENDS.md, WILDLIFE_LIBERATION.md and SWITCHBACK.md have each now recorded
# independently, on four different modes, which is why the assert below fails the build rather than
# trusting the number. At 0.7 a quarter-of-course deficit (3.5 stations) buys 2.45 element levels.
# Higher than Switchback's 0.5 against a shorter course on purpose: the deficit is measured on the
# LEAD RUNNER, and here arriving first also wins you the UNDAMAGED plug and the choice of how to
# open it, so falling behind compounds in a way a plain gate race's does not.
COMEBACK_RATE = arena.COMEBACK_RATE

SHELL_INNER = arena.SHELL_INNER               # 420
SHELL_OUTER = arena.SHELL_OUTER               # 1080
SPAWN_RING_RADIUS = arena.SPAWN_RING_RADIUS   # 480

# Crystals respawn on a sphere of this radius because the cell authors NO NUCLEUS, and
# CrystalManager.GetAnchorlessSpawnRadius falls through nucleus -> noNucleusSpawnRadius ->
# the crystal's own SphereRadius, which would stack every one of them on the arena's exact centre.
# 900 sits inside the course shell (420..1080), which is the point: crystals are not this mode's
# ammunition (the arena is), so they belong AMONG the stations where a pilot passes them anyway -
# which is exactly what BreakwaterController.aiCrystalDetourSlack is for.
CRYSTAL_SPAWN_RADIUS = 900

# The mode's own arena is the OUTERMOST thing in this cell, and the cell must be able to SENSE it:
# Cell.SenseRadius bounds prism registration (ContainsPosition), so anything past it is mass the
# cell does not know it holds. A station at the shell's outer edge carries its dish 1.75 x R_port
# further out, and a shoal cluster can sit shoalOffsetMax + its own radius off a leg - so the
# radius is DERIVED from the arena the model prices, then rounded up to a 50-unit step.
_DISH_REACH = arena.DISH_RATIO * max(c["port"] for c in arena.INTENSITIES)


def _cell_config_index(intensity: int) -> str:
    return f"asset/BreakwaterCellConfig{intensity}"


# ── New script GUIDs (the .cs.meta files this script also writes) ────────────────────────────
#
# NOT here, and deliberately: RaceGateRing.cs.meta and Assets/_Scripts/Controller/Arcade/Racing.meta
# are owned by author_switchback_assets.py and are already committed with the guids it mints. That
# generator's own comment says a second generator emitting the folder must reuse ITS guid; the
# honest reading is that a second generator must not emit it at all, because two owners of one file
# flip it on alternate runs. Asserted below (RACING_OWNED_ELSEWHERE).
G_SCRIPT = {
    "BreakwaterCourse":             guid("script/BreakwaterCourse"),
    "BreakwaterStationBuilder":     guid("script/BreakwaterStationBuilder"),
    "BreakwaterController":         guid("script/BreakwaterController"),
    "SpawnableBreakwater":          guid("script/SpawnableBreakwater"),
    "BreakwaterCourseTests":        guid("script/BreakwaterCourseTests"),
}

G_ASSET = {
    "ArcadeGameBreakwater":      guid("asset/ArcadeGameBreakwater"),
    "BreakwaterScoringRule":     guid("asset/BreakwaterScoringRule"),
    "MinigameBreakwater.unity":  guid("asset/MinigameBreakwater.unity"),
    "SpawnableBreakwater.prefab": guid("asset/SpawnableBreakwater.prefab"),
    "BreakwaterSpawnProfile":    guid("asset/BreakwaterSpawnProfile"),
    "BreakwaterCellFolder":      guid("folder/BreakwaterCell"),
    "BreakwaterFolder":          guid("folder/Breakwater"),
}
for _i in (1, 2, 3, 4):
    G_ASSET[f"BreakwaterCellConfig{_i}"] = guid(_cell_config_index(_i))

# ── Existing GUIDs we reference (read out of the repo, never invented) ───────────────────────
EXISTING = {
    "SO_ArcadeGame":            "fe040efad3307fb449b6b72ad15362da",
    "CellConfigDataSO":         "01f934d50526431a9392a6ceca1dc33d",
    "SpawnProfileSO":           "e8d8aa5d835249798a256e18f2f7d912",
    # the scoring rule SCRIPT is Switchback's - a second asset, not a second class
    "GateRaceScoringRuleSO":   "349cc0c9402590262de23356775d43cc",
    "RaceGateTurnMonitor":     "da1e0d6121acc091283ec785f932c32c",
    # donor scene wiring to swap out
    "SalvoController":          "b406a35c42d10f9370f84601bf14c5c1",
    "SalvoPrismTurnMonitor":    "52fe72c1598aa36a548373e88c0ac2ca",
    "SalvoScoringRule":         "0eef89a2be7db8524521ff61f06122ce",
    "BoneyardCell1":            "cc21823da49bd38535c54796c4a1c168",
    "BoneyardCell2":            "64160342670283d2a0c5088f1951295c",
    "BoneyardCell3":            "7dc0173b8f31a743221e8f0bd3ec2d40",
    "BoneyardCell4":            "5511269da8da60affaf693e51b8a60a1",
    # shared content
    "Vessel_Sparrow":           "7b7053dd065edb54baa3b831b90f4985",
    "CellRuntimeData":          "8d4e8398eedc76c4dadb8604f89b9e1b",
    "CapsuleMembrane":          "6e330f85972faf843b8a128e7166f7b5",
    "Cytoplasm":                "9cacd903fcf4643459f5f14ac811bb20",
    "CellIcon":                 "6aa1c06e11b265744a5f9fa8858ac72a",
    "EnvironmentPrism":         "ed9defc56162b4b4588e61c20984b6d9",
    # arcade card art, shared with the other pure-race cards (Switchback uses the same three)
    "IconActive":               "1dc25875d7cbd3e478fc5a133e65eedb",
    "IconInactive":             "fa9b62abd1b217b4ba3d7c5a4a2c0916",
    "CardBackground":           "587d2203114c8004c9985d0112c89585",
}

RACING_OWNED_ELSEWHERE = {
    "Assets/_Scripts/Controller/Arcade/Racing.meta": "a9cb4dd42aeabbdb0dd58cfdce2df22b",
    "Assets/_Scripts/Controller/Arcade/Racing/RaceGateRing.cs.meta":
        "5c7998f7c6ab3b62b2368c80a15e7413",
}

MEMBRANE_FILEID = 346633111830028674
CYTOPLASM_FILEID = 639495419069806261
PRISM_FILEID = 4563009547826722997
CELL_SCENE_FILEID = 1700000065          # four components reference the Cell by this id

# The Spawnables family's internal fileID triple (SpawnableOrrery, SpawnableDrum, the five
# SpawnableRibcage variants all use it). fileIDs are scoped per asset, so sharing is correct.
PF_GO, PF_TR, PF_MB = 5230000000000101, 5230000000000102, 5230000000000103

SCENE_PATH = "Assets/_Scenes/Multiplayer Scenes/MinigameBreakwater.unity"
DONOR_SCENE_PATH = "Assets/_Scenes/Multiplayer Scenes/MinigameSalvo.unity"
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Breakwater Cell"


# ── YAML helpers ─────────────────────────────────────────────────────────────────────────────

_HEADER_TMPL = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: __GUID__, type: 3}
  m_Name: __NAME__
  m_EditorClassIdentifier:
"""


def HEADER_FOR(script_guid: str, name: str) -> str:
    return _HEADER_TMPL.replace("__GUID__", script_guid).replace("__NAME__", name)


def meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n"
            f"  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n"
            f"  icon: {{instanceID: 0}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def asset_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            f"  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n"
            f"  assetBundleVariant:\n")


def scene_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nDefaultImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def prefab_meta(g: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {g}\nPrefabImporter:\n  externalObjects: {{}}\n"
            f"  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def folder_meta(g: str) -> str:
    # A directory under Assets/ is itself an asset. Without this Unity mints a fresh guid on every
    # machine and the folder shows as an untracked change forever.
    return (f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n"
            f"  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def yaml_text(key: str, text: str, width: int = 82) -> str:
    """Fold prose into Unity's multi-line PLAIN scalar shape (`  Key: first line` + 4-space
    continuations). Plain scalars cannot contain ": " or " #", and a continuation may not begin
    with "- " - all three would re-parse as structure. Checked rather than trusted: one shipped
    cell Description already carries a ": " that only Unity's lenient reader forgives."""
    flat = " ".join(text.split())
    require(": " not in flat, f"{key}: prose contains ': ', which breaks a YAML plain scalar")
    require(" #" not in flat, f"{key}: prose contains ' #', which starts a YAML comment")

    words = flat.split(" ")
    lines, cur = [], f"  {key}:"
    limit_first, limit_rest = width, width - 2
    for i, word in enumerate(words):
        limit = limit_first if not lines else limit_rest
        fits = cur.endswith(":") or len(cur) + 1 + len(word) <= limit
        # A dash that would LAND FIRST on a continuation line is kept on the previous line even
        # though it overflows: "- " at the head of a line re-parses as a YAML list item, and the
        # alternative (banning dashes from prose) would make the prose worse to read for a reason
        # that belongs to the serializer.
        starts_a_dash = (not fits) and (word == "-" or (word.startswith("-") and len(word) > 1
                                                        and not word[1].isdigit()))
        if fits or starts_a_dash:
            cur = f"{cur} {word}"
        else:
            lines.append(cur)
            cur = f"    {word}"
    lines.append(cur)
    for ln in lines[1:]:
        require(not ln.strip().startswith("-"),
                f"{key}: a folded line begins with '-' and would parse as a list item")
    block = "\n".join(lines) + "\n"
    folded_blocks.append((key, flat, block))
    return block


def num(x) -> str:
    """Unity writes a whole float without a decimal point; matching that keeps a Unity re-save
    from showing up as drift on the very next --check."""
    if isinstance(x, float) and x.is_integer():
        return str(int(x))
    return f"{x:g}"


# ══ 1. .cs.meta for the new scripts, and the folder's own ════════════════════════════════════
SCRIPT_PATHS = {
    "BreakwaterCourse":
        "Assets/_Scripts/Controller/Arcade/Breakwater/BreakwaterCourse.cs",
    "BreakwaterStationBuilder":
        "Assets/_Scripts/Controller/Arcade/Breakwater/BreakwaterStationBuilder.cs",
    "BreakwaterController":
        "Assets/_Scripts/Controller/Arcade/Breakwater/BreakwaterController.cs",
    "SpawnableBreakwater":
        "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableBreakwater.cs",
    # A test lives under a folder literally named Editor (Assembly-CSharp-Editor), never its own
    # asmdef - an asmdef cannot reference Assembly-CSharp, so the suite would be blind to every
    # gameplay type it tests, and a test outside Editor/ reaches the IL2CPP linker and breaks the
    # Windows player build at `Failed to resolve assembly: nunit.framework`.
    "BreakwaterCourseTests":
        "Assets/_Scripts/Tests/Editor/BreakwaterCourseTests.cs",
}
for _k, _p in SCRIPT_PATHS.items():
    emit(_p + ".meta", meta(G_SCRIPT[_k]))

emit("Assets/_Scripts/Controller/Arcade/Breakwater.meta", folder_meta(G_ASSET["BreakwaterFolder"]))

# The mode doc is an ASSET too. Without a .meta Unity mints a fresh GUID for it on every machine
# and validate_project.py flags the churn - the same reason author_switchback_assets.py owns
# SWITCHBACK.md.meta. The doc's own text belongs to whoever writes it; only its identity is ours.
BREAKWATER_DOC = "Assets/_Scripts/Controller/Arcade/BREAKWATER.md"
emit(BREAKWATER_DOC + ".meta",
     f"fileFormatVersion: 2\nguid: {guid('doc/BREAKWATER.md')}\nTextScriptImporter:\n"
     f"  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# ══ 2. Scoring rule: a SECOND ASSET on GateRaceScoringRuleSO ════════════════════════════════
#
# metric 9 = ScoringMetric.SwitchesThreaded, read from the enum. That class already reads its metric
# plus GameDataSO.SwitchTargetCount and already overrides DomainValue -> BestByDomain, which is
# exactly this mode's rule: every pilot flies the same course, so summing teammates would hand a
# two-pilot domain twice the course. golfRules 1 for the same reason Switchback's is: the winning
# domain's pilots carry a finish time and everyone else a sentinel, so lower is better.
#
# unitNoun is what the SCOREBOARD and the defeat reveal call one unit of this course. Three modes
# read this rule - Switchback and Headlong fly GATES, Breakwater flies STATIONS - so the noun is
# authored per ASSET rather than hardcoded in a script all three share. It is authored EXPLICITLY
# and not left to a C# field initializer: Unity fills a key an asset does not carry with the TYPE
# default (an empty string), never with the initializer, which is why the fallback to "Gate" lives
# in the rule's READER. The two older assets carry no noun and are deliberately not rewritten here.
emit("Assets/_SO_Assets/Scoring Rules/BreakwaterScoringRule.asset",
     HEADER_FOR(EXISTING["GateRaceScoringRuleSO"], "BreakwaterScoringRule") +
     f"  unitNoun: Station\n  metric: {METRIC_SWITCHES_THREADED}\n  golfRules: 1\n")
emit("Assets/_SO_Assets/Scoring Rules/BreakwaterScoringRule.asset.meta",
     asset_meta(G_ASSET["BreakwaterScoringRule"]))


# ══ 3. The SpawnableBreakwater prefab (ONE - see the module docstring) ═══════════════════════
#
# domain 3 = Domains.Blue, the "no team" sentinel. StatsManager.IsFriendlyEnvironmentPrism is
# domain-only, so Blue is hostile to EVERY domain: every pilot's rounds pay ammunition on every
# door. It also closes the Wildlife Liberation trap - the Sparrow's Charge-5 upgrade spares
# own-domain mass, so a station in a playable colour would be literally unopenable by that domain,
# via an upgrade the comeback system hands to whoever is LOSING.
#
# layAcrossFrames 0 because the stations are gameplay-critical: they must fully exist the moment
# Spawn returns, which is the case SpawnableBase's own tooltip names ("race tracks, courses").
SHOAL = dict(offset_min=40, offset_max=110, station_clearance=40, spline_clearance=30,
             clump_radius=8, placement_attempts=12, spawn_pad_clearance=60)

emit("Assets/_Prefabs/Spawnables/SpawnableBreakwater.prefab", f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{PF_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {PF_TR}}}
  - component: {{fileID: {PF_MB}}}
  m_Layer: 0
  m_Name: SpawnableBreakwater
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{PF_TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {PF_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{PF_MB}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {PF_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {G_SCRIPT['SpawnableBreakwater']}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: {MODE_BREAKWATER}
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: 1
  prism: {{fileID: {PRISM_FILEID}, guid: {EXISTING['EnvironmentPrism']}, type: 3}}
  density: 1
  spawnClearRadius: 0
  spawnClearPoints: []
  shoalOffsetMin: {SHOAL['offset_min']}
  shoalOffsetMax: {SHOAL['offset_max']}
  shoalStationClearance: {SHOAL['station_clearance']}
  shoalSplineClearance: {SHOAL['spline_clearance']}
  shoalClumpRadius: {SHOAL['clump_radius']}
  shoalPlacementAttempts: {SHOAL['placement_attempts']}
  shoalSpawnPadClearance: {SHOAL['spawn_pad_clearance']}
""")
emit("Assets/_Prefabs/Spawnables/SpawnableBreakwater.prefab.meta",
     prefab_meta(G_ASSET["SpawnableBreakwater.prefab"]))

_SHOAL_REACH = (SHOAL["offset_max"]
                + SHOAL["clump_radius"] * arena.SHOAL_CUBE * 0.35   # ClumpRadiusFactorMax = 1.4
                + arena.SHOAL_CUBE * 0.5 * math.sqrt(3.0))
SENSE_RADIUS = int(math.ceil((SHELL_OUTER + max(_DISH_REACH, _SHOAL_REACH)) / 50.0) * 50)


# ══ 4. The cell: four intensities, no nucleus, no environment prefab, no life ════════════════
emit(CELL_DIR + ".meta", folder_meta(G_ASSET["BreakwaterCellFolder"]))

# ONE spawn profile for all four configs. Its entire content is "nothing lives here", which cannot
# differ per intensity - four copies would be four places for that to drift.
#
# EVERY field the class declares is authored explicitly, including the ones whose C# initializer is
# non-zero. Unity fills a MISSING key with the TYPE default, not the field initializer, so an
# omitted FloraPopulationScale deserializes as 0 (not 1) and an omitted InitialFaunaReleaseTier as
# 0 (not int.MaxValue) - a biome that silently seals itself. The shipped Drumfire profile omits
# eight of these; this one omits none. Field order follows the C# declaration order, which is the
# order Unity itself writes on a re-save.
emit(f"{CELL_DIR}/Breakwater Spawn Profile.asset",
     HEADER_FOR(EXISTING["SpawnProfileSO"], "Breakwater Spawn Profile") + """  FloraExcludeLocalDomain: 0
  FloraSpawnVolumeCeiling: 12000
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  FloraPopulationScale: 1
  FloraPrismScale: 1
  FloraPlantBudgetScale: 1
  SupportedFloras: []
  FaunaExcludeLocalDomain: 0
  InitialFaunaSpawnWaitTime: 10
  InitialFaunaReleaseTier: 2147483647
  FaunaSpawnVolumeThreshold: 1
  FaunaPopulationScale: 1
  BaseFaunaSpawnTime: 30
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 5
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 0
  HerbivoreSpawnPointCount: 0
  HerbivoreSpawnRadius: 400
  PredatorSpawnPointCount: 0
  PredatorSpawnRadius: 600
  SupportedFaunas: []
""")
emit(f"{CELL_DIR}/Breakwater Spawn Profile.asset.meta", asset_meta(G_ASSET["BreakwaterSpawnProfile"]))

CELL_DESCRIPTION_TAIL = (
    "No nucleus (the stations are the landmarks, and this cell's crystals respawn on "
    "noNucleusSpawnRadius instead), no EnvironmentPrefab (the course is rolled per match, so "
    "BreakwaterController stands the arena up itself once the poses exist), and no flora or fauna "
    "- in a nucleus-less cell herbivores eat opposing-domain mass and the whole arena is Blue, so "
    "a food web would graze the plugs open on its own. PhaseThresholds ride THIS intensity's own "
    "measured baseline; regenerate with Tools/Build/author_breakwater_assets.py rather than "
    "hand-editing.")

CELL_TOTALS = []
for _idx in range(4):
    _cfg = arena.INTENSITIES[_idx]
    _tot = arena.arena_totals(_idx)
    _th = arena.phase_thresholds(_tot["volume"], _tot["prisms"])
    CELL_TOTALS.append((_cfg, _tot, _th))

    _intensity = _idx + 1
    _name = f"Breakwater Cell Config {_intensity}"
    _desc = (
        f"The Breakwater arena at intensity {_intensity} - {_tot['prisms']:,} prisms across "
        f"{STATION_TARGET} stations of port radius {_cfg['port']:.0f}, plus "
        f"{_tot['shoal_prisms']} prisms of shoal rubble strung along the legs as ammunition. "
        f"Each station is a {arena.plug_bars(_cfg['port'])[0]}-bar danger weave over an "
        f"{arena.EYE_RADIUS:.0f}-unit eye, ringed by a {arena.COLLAR_COUNT}-block collar inside a "
        f"{arena.dish_plates(_cfg['port'])}-plate dish. Everything is Domains.Blue, so it is "
        f"hostile to every pilot and every round pays ammunition. " + CELL_DESCRIPTION_TAIL)

    emit(f"{CELL_DIR}/{_name}.asset",
         HEADER_FOR(EXISTING["CellConfigDataSO"], _name)
         + "  CellName: Breakwater\n"
         + yaml_text("Description", _desc)
         + f"""  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: {_intensity}
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_FILEID}, guid: {EXISTING['CapsuleMembrane']}, type: 3}}
  NucleusPrefab: {{fileID: 0}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM_FILEID}, guid: {EXISTING['Cytoplasm']}, type: 3}}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['BreakwaterSpawnProfile']}, type: 2}}
  EnvironmentPrefab: {{fileID: 0}}
  EnvironmentIntensity: {_intensity}
  SenseRadiusOverride: {SENSE_RADIUS}
  PhaseThresholds:
    RestlessEnter: {_th['RestlessEnterCount']}
    RestlessExit: {_th['RestlessExitCount']}
    FrenzyEnter: {_th['FrenzyEnterCount']}
    FrenzyExit: {_th['FrenzyExitCount']}
    RestlessEnterVolume: {round(_th['RestlessEnterVolume'])}
    RestlessExitVolume: {round(_th['RestlessExitVolume'])}
    FrenzyEnterVolume: {round(_th['FrenzyEnterVolume'])}
    FrenzyExitVolume: {round(_th['FrenzyExitVolume'])}
""")
    emit(f"{CELL_DIR}/{_name}.asset.meta", asset_meta(G_ASSET[f"BreakwaterCellConfig{_intensity}"]))


# ══ 5. Arcade card ═══════════════════════════════════════════════════════════════════════════
#
# SPARROW ONLY: a single entry in Vessels drives all three enforcement layers (the launcher clamp,
# the server-side spawn clamp and the AI clamp). MinPlayers/MinDomains 2 because a race needs a
# rival - with one domain the objective is reached the moment anyone finishes and there is nobody
# to have beaten. GolfScoring 1 to match the rule asset.
#
# NO CallToActionTargetType and NO PreviewClip: neither is a field on SO_ArcadeGame or SO_Game. Both
# survive in older generators (and, unsaved, in ArcadeGameSalvo.asset on disk) purely because those
# scripts' key validators carried a hand-written whitelist. Ours derives the allowed set from the
# C#, so a retired key cannot be authored here without failing the run.
CARD_DESCRIPTION = (
    "Sparrows only, against fourteen walls. Every station is a dish of plates with its throat "
    "welded shut - a triple weave of danger bars around an eye barely wider than your hull. You "
    "arrive with two rockets and one choice - blow a door, stop dead and saw one open in turret "
    "stance, or thread the eye and take nothing with you. Prisms buy rockets, so the wall you just "
    "opened pays for the next one. First team to put a pilot through the last station takes it.")

emit("Assets/_SO_Assets/Games/ArcadeGameBreakwater.asset",
     HEADER_FOR(EXISTING["SO_ArcadeGame"], "ArcadeGameBreakwater")
     + f"  Mode: {MODE_BREAKWATER}\n"
     + "  IsMultiplayer: 1\n"
     + "  DisplayName: Breakwater\n"
     + yaml_text("Description", CARD_DESCRIPTION)
     + f"""  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  GolfScoring: 1
  SceneName: MinigameBreakwater
  Vessels:
  - {{fileID: 11400000, guid: {EXISTING['Vessel_Sparrow']}, type: 2}}
  MinPlayersAllowed: 2
  MaxPlayersAllowed: 4
  MinDomainsAllowed: 2
  MaxDomainsAllowed: 3
  MinIntensity: 1
  MaxIntensity: 4
  Tips: []
  PreviewVideo: {{fileID: 0}}
  ViewUserAction: 0
  PlayUserAction: 0
  ComebackRatePerScoreDeficit: {num(COMEBACK_RATE)}
""")
emit("Assets/_SO_Assets/Games/ArcadeGameBreakwater.asset.meta",
     asset_meta(G_ASSET["ArcadeGameBreakwater"]))


# ══ 6. The scene ═════════════════════════════════════════════════════════════════════════════
#
# Cloned ONCE from MinigameSalvo - the other Sparrow-only mode, and already wired for the four
# things this mode needs: an all-Sparrow AI roster (vesselClass 11 x4), a NUCLEUS-LESS cell on an
# IntensityWise ladder, an authored noNucleusSpawnRadius, and a cell-relative spawn ring.
#
# SAFETY RULE (b): once the scene has shipped, the file on disk is UNITY'S. Saving a scene
# re-derives every in-scene NetworkObject's GlobalObjectIdHash, and a generator that rewrites the
# file puts the donor's value back - which is a cross-peer scene-sync break with nothing in the
# console (measured on MinigameSwitchback: 2537121143 -> 99744438). So the clone is a bootstrap,
# and from then on the shipped file is registered READ-ONLY and VALIDATED instead of rewritten.
SCENE_ALREADY_SHIPPED = exists(SCENE_PATH)

# The donor's SalvoController field block. Everything from `rule` down belongs to SalvoController
# itself; numberOfRounds / numberOfTurnsPerRound / countdownTimer / _onToggleReadyButton above it
# are MiniGameControllerBase's and are shared, so they stay.
DONOR_CONTROLLER_FIELDS = f"""  rule: {{fileID: 11400000, guid: {EXISTING['SalvoScoringRule']}, type: 2}}
  arenaCell: {{fileID: {CELL_SCENE_FILEID}}}
  onOmniCrystalCollected: {{fileID: 11400000, guid: 3664bc230593b734aa52dd67e3caa21c,
    type: 2}}
  missileResourceIndex: 0
  elementalCrystalCount: 14
  crystalScatterRadius: 400
  crystalScatterSeed: 42
"""

# BreakwaterController's own serialized fields, in DECLARATION ORDER (which is the order Unity
# writes them, so a re-save is not drift). Every value is the C# field initializer except `rule`,
# `cellData` and `arenaPrefab`, which are references the inspector must carry - the C# defaults
# and these are cross-checked below so the two can never disagree.
NEW_CONTROLLER_FIELDS = f"""  rule: {{fileID: 11400000, guid: {G_ASSET['BreakwaterScoringRule']}, type: 2}}
  cellData: {{fileID: 11400000, guid: {EXISTING['CellRuntimeData']}, type: 2}}
  arenaPrefab: {{fileID: {PF_MB}, guid: {G_ASSET['SpawnableBreakwater.prefab']}, type: 3}}
  courseOuterRadius: {num(SHELL_OUTER)}
  courseInnerRadiusFallback: {num(SHELL_INNER)}
  ringBloomSeconds: 0.9
  courseSeed: 0
  aiCommitDistance: 240
  aiApproachLead: 280
  aiThroughDistance: 200
  aiCrystalDetourSlack: 200
  aiCrystalScanSeconds: 0.5
  maxPlausibleSpeed: 400
  reportResyncSeconds: 3
"""

DONOR_CELL_BLOCK = ("  CellConfigs:\n"
                    + "".join(f"  - {{fileID: 11400000, guid: {EXISTING[k]}, type: 2}}\n"
                              for k in ("BoneyardCell1", "BoneyardCell2",
                                        "BoneyardCell3", "BoneyardCell4"))
                    + "  cellTypeChoiceOptions: 1\n")

# cellTypeChoiceOptions STAYS 1 (IntensityWise, list order = intensity). Unlike Switchback, whose
# intensity is purely the course, Breakwater's intensity moves the ARENA too - the port radius sets
# the door, the dish and therefore the mass, so each level needs its own measured PhaseThresholds.
NEW_CELL_BLOCK = ("  CellConfigs:\n"
                  + "".join(f"  - {{fileID: 11400000, guid: "
                            f"{G_ASSET[f'BreakwaterCellConfig{i}']}, type: 2}}\n"
                            for i in (1, 2, 3, 4))
                  + "  cellTypeChoiceOptions: 1\n")

# Every patch is a literal-for-literal replacement with an exact-occurrence assertion, so a re-run
# on an unchanged donor is byte-identical (safety rule (b)) and a donor that has moved on is
# reported by NAME rather than silently producing a half-patched scene.
SCENE_PATCHES = [
    # (what, old, new, expected occurrences)
    # The SHARED monitor: Breakwater is a GateRaceController, so RaceGateTurnMonitor reads
    # its AuthoritativeGateCount exactly as it does Switchback's and Headlong's.
    ("turn monitor script", EXISTING["SalvoPrismTurnMonitor"],
     EXISTING["RaceGateTurnMonitor"], 1),
    ("controller script", EXISTING["SalvoController"], G_SCRIPT["BreakwaterController"], 1),
    ("controller field block", DONOR_CONTROLLER_FIELDS, NEW_CONTROLLER_FIELDS, 1),
    ("Cell config list", DONOR_CELL_BLOCK, NEW_CELL_BLOCK, 1),
    # A cell with NO NUCLEUS must author this or CrystalManager.GetAnchorlessSpawnRadius falls
    # through to the crystal's own SphereRadius and stacks every crystal on the exact centre.
    ("crystal respawn radius", "  noNucleusSpawnRadius: 420\n",
     f"  noNucleusSpawnRadius: {CRYSTAL_SPAWN_RADIUS}\n", 1),
    # THE FAIRNESS RULE. Station 1 sits on the spawn formation's POLE, so every pilot is exactly
    # sqrt(spawnRadius^2 + d^2) from it. Under the donor's Symmetric (tetrahedral) formation no
    # such point exists and whoever spawned nearest station 1 starts ahead - which matters more
    # here than in a plain gate race, because arriving first also wins the UNDAMAGED plug.
    ("spawn formation", "  spawnFormation: 0\n", "  spawnFormation: 1\n", 1),
    ("spawn ring floor", "  spawnRingRadiusFloor: 700\n",
     f"  spawnRingRadiusFloor: {int(SPAWN_RING_RADIUS)}\n", 1),
    # ElementalComebackSystem.EnsureExists respects a scene-authored instance AS AUTHORED - it only
    # fills in gameData - so DefaultSourceFor runs on the AddComponent branch alone. Leaving the
    # donor's PrismsDestroyed (3) would point the comeback at a stat no pilot in this mode moves,
    # and would make every Breakwater case in that file unreachable dead code.
    ("comeback source", "  differenceSource: 3\n",
     f"  differenceSource: {COMEBACK_SWITCHES_THREADED}\n", 1),
    ("comeback golf flag", "  useGolfRules: 0\n", "  useGolfRules: 1\n", 1),
]

if SCENE_ALREADY_SHIPPED:
    scene = read(SCENE_PATH)
    scene_source = "shipped (read-only)"
    notes.append(
        f"{SCENE_PATH} already exists, so the clone step stood down and the shipped file was "
        f"registered read-only. Unity owns that file's GlobalObjectIdHash values; the WIRING "
        f"checks below are what gate it, not the byte diff. To re-bootstrap from the donor, "
        f"delete the scene and re-run.")
else:
    scene = read(DONOR_SCENE_PATH)
    scene_source = f"cloned from {os.path.basename(DONOR_SCENE_PATH)}"
    for what, old, new, want in SCENE_PATCHES:
        got = scene.count(old)
        if got != want:
            errors.append(f"scene clone: {what} matched {got} time(s), expected {want} - the "
                          f"donor {os.path.basename(DONOR_SCENE_PATH)} has moved on. Re-derive the "
                          f"patch, or pick a donor that still carries it.")
            continue
        scene = scene.replace(old, new)

emit(SCENE_PATH, scene)
emit(SCENE_PATH + ".meta", scene_meta(G_ASSET["MinigameBreakwater.unity"]))


# ══ 7. Registrations ═════════════════════════════════════════════════════════════════════════

# 7a. the party-games list. A reference row has nothing inside it to drift, so presence IS content -
# but the count is asserted, because a duplicate lists the card twice in the picker.
LIST_PATH = "Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset"
games = read(LIST_PATH)
entry = f"  - {{fileID: 11400000, guid: {G_ASSET['ArcadeGameBreakwater']}, type: 2}}\n"
if entry not in games:
    require(games.endswith("\n"), f"{LIST_PATH} does not end in a newline")
    games = games + entry
require(games.count(entry) == 1,
        "the Breakwater card is listed more than once in OrganicRematchGames")
emit(LIST_PATH, games)

# 7b. always-unlocked, so the card is clickable on a fresh account
PROG_PATH = "Assets/_SO_Assets/GameModeQuest/ProgressionConfig.asset"
prog = read(PROG_PATH)
if re.search(rf"^  alwaysUnlockedModes:\n(?:  - \d+\n)*  - {MODE_BREAKWATER}\n", prog, re.M) is None:
    prog, n = re.subn(r"(  alwaysUnlockedModes:\n(?:  - \d+\n)*)",
                      rf"\g<1>  - {MODE_BREAKWATER}\n", prog, count=1)
    require(n == 1, "alwaysUnlockedModes block not found in ProgressionConfig")
emit(PROG_PATH, prog)

# 7c. build settings, anchored after MinigameSalvo (the donor, and this mode's nearest sibling)
BUILD_PATH = "ProjectSettings/EditorBuildSettings.asset"
build = read(BUILD_PATH)
if "MinigameBreakwater.unity" not in build:
    anchor = re.search(
        r"(  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/MinigameSalvo\.unity\n"
        r"    guid: [0-9a-f]{32}\n)", build)
    if require(anchor is not None, "Salvo scene entry not found in EditorBuildSettings"):
        build = build.replace(
            anchor.group(1), anchor.group(1)
            + "  - enabled: 1\n    path: Assets/_Scenes/Multiplayer Scenes/"
              "MinigameBreakwater.unity\n"
            + f"    guid: {G_ASSET['MinigameBreakwater.unity']}\n")
emit(BUILD_PATH, build)

# 7d. the end-game target. SET semantics, never insert-if-absent: with insert-if-absent the emitted
# buffer equals the disk buffer on every run after the first, so --check would pass whatever the
# number had drifted to and a retune here would be a silent no-op (the Dog Fight generator's
# lesson). Both the live key and its BUILD twin, so a build-time restore lands on the same number.
END_PATH = "Assets/Resources/EndConditionOverrides.asset"
endcond = read(END_PATH)
for sibling, new_key in (("switchbackGateTarget", "breakwaterStationTarget"),
                         ("switchbackGateTargetBuild", "breakwaterStationTargetBuild"),
                         ("breakwaterStationTarget", "breakwaterLaps"),
                         ("breakwaterStationTargetBuild", "breakwaterLapsBuild")):
    row = (f"  {new_key}: {LAPS}\n" if new_key.startswith("breakwaterLaps")
           else f"  {new_key}: {STATION_TARGET}\n")
    already = re.search(rf"^  {new_key}: \d+\n", endcond, re.M)
    if already:
        endcond = endcond.replace(already.group(0), row, 1)
        continue
    # Placed immediately after its sibling because that is where the C# declares it, so a Unity
    # re-save writes the file in the order this script already wrote it.
    m = re.search(rf"^  {sibling}: \d+\n", endcond, re.M)
    if require(m is not None, f"{sibling} not found in {END_PATH}"):
        endcond = endcond.replace(m.group(0), m.group(0) + row, 1)
emit(END_PATH, endcond)


# ══════════════════ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ══════════════════════════════

# ── The comeback rate is meaningless without the target beside it ────────────────────────────
_quarter = 0.25 * CROSSING_TARGET * COMEBACK_RATE
require(_quarter >= 1.0,
        f"comeback rate {COMEBACK_RATE} is dead against target {CROSSING_TARGET}: a "
        f"quarter-of-target deficit buys {_quarter:.2f} element levels (< 1). Rescale the rate "
        f"with the target - `bonusLevels = deficit x rate`.")
require(_quarter <= 5.0,
        f"comeback rate {COMEBACK_RATE} hands the trailing domain {_quarter:.1f} element levels "
        f"for a quarter-of-target deficit")

# ── The three copies of the station count must agree ─────────────────────────────────────────
_endcond_cs = "Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs"
_default_target = cs_const(_endcond_cs, "DefaultBreakwaterStationTarget")
require(_default_target == STATION_TARGET,
        f"EndConditionOverridesSO.DefaultBreakwaterStationTarget ({_default_target}) != the "
        f"model's STATION_COUNT ({STATION_TARGET}) - the course a pilot flies and the number "
        f"counting it would be different numbers")
require(STATION_TARGET >= 2, "a course of fewer than two stations is not a course")

_default_laps = cs_const(_endcond_cs, "DefaultBreakwaterLaps")
require(_default_laps == LAPS,
        f"EndConditionOverridesSO.DefaultBreakwaterLaps ({_default_laps}) != the model's LAPS "
        f"({LAPS})")
_course_cs_laps = cs_const("Assets/_Scripts/Controller/Arcade/Breakwater/BreakwaterCourse.cs",
                           "DefaultLaps")
require(_course_cs_laps == LAPS,
        f"BreakwaterCourseSettings.DefaultLaps ({_course_cs_laps}) != the model's LAPS ({LAPS})")

# The fold is the whole of the laps feature, so the model's own arithmetic is asserted here
# rather than trusted: the start gate once, then the circuit forward once per lap, and every
# crossing naming a station that exists.
_visited = [arena.ring_for_crossing(t) for t in range(CROSSING_TARGET)]
_expected = [0] + list(range(1, STATION_TARGET)) * LAPS
require(_visited == _expected,
        f"the circuit fold is not the expected sequence: {_visited} != {_expected}")
require(all(0 <= i < STATION_TARGET for i in _visited),
        "a crossing names a station the course does not lay")
# NEVER TWICE IN A ROW - a crossing that repeated the ring it just paid is not a crossing a pilot
# can fly, and it is the thing the out-and-back fold had to work around at its turnaround.
require(all(a != b for a, b in zip(_visited, _visited[1:])),
        "the fold repeats a ring back to back")
require(_visited.count(0) == 1, "the start gate must be threaded exactly once")

# ── THE SUBCLASS ACTUALLY FITS THE BASE ─────────────────────────────────────────────────────
#
# BreakwaterController is a GateRaceController, and NOTHING ELSE IN THIS REPO CAN CHECK THAT
# out of the editor. A dotnet syntax pass over these files leaves the base type unresolved, and
# Roslyn abandons class-body binding when a base type is unresolved - so a `protected override`
# naming a member the base does not declare, or a missing implementation of an abstract one, is
# reported as NOTHING (CLAUDE.md records this exact blind spot for enum members). The check is
# therefore structural and textual, and it is the whole safety net for the platform adoption.
_base_cs = "Assets/_Scripts/Controller/Arcade/Racing/GateRaceController.cs"
_ctrl_cs = "Assets/_Scripts/Controller/Arcade/Breakwater/BreakwaterController.cs"


def _members(src, pattern):
    """(name) for every member declaration matching pattern - properties and methods alike."""
    out = set()
    for m in re.finditer(pattern, src):
        out.add(m.group("name"))
    return out


if exists(_base_cs) and exists(_ctrl_cs):
    _base = read(_base_cs)
    _ctrl = read(_ctrl_cs)

    _abstract = _members(_base, r"\b(?:public|protected|internal)\s+abstract\s+[\w<>,\[\]\. ]+?\s+(?P<name>\w+)\s*[({=]")
    _virtual = _members(_base, r"\b(?:public|protected|internal)\s+virtual\s+[\w<>,\[\]\. ]+?\s+(?P<name>\w+)\s*[({=]")
    _overrides = _members(_ctrl, r"\b(?:public|protected|internal)\s+override\s+[\w<>,\[\]\. ]+?\s+(?P<name>\w+)\s*[({=]")

    require(_abstract, "GateRaceController declares no abstract members - the pattern moved and "
                       "this check is now vacuous")

    _missing = sorted(_abstract - _overrides)
    require(not _missing,
            f"BreakwaterController does not implement GateRaceController's abstract member(s) "
            f"{_missing} - it would not compile, and nothing outside the editor would say so")

    _unknown = sorted(_overrides - _abstract - _virtual)
    require(not _unknown,
            f"BreakwaterController overrides {_unknown}, which GateRaceController declares neither "
            f"abstract nor virtual. Either the base member was renamed upstream or this is a "
            f"leftover from the pre-adoption controller")

    # The lead-in is the ONE thing this mode asked the platform for. Its absence is a silently
    # wrong race - the start gate re-offered on lap two, which is the defect the circuit fixed.
    require("LeadInGates" in _ctrl and "LeadInGates" in _base,
            "BreakwaterController must override LeadInGates (its start gate is threaded once) and "
            "GateRaceController must declare it")

    # NEGATIVE CONTROLS: a gate nobody has watched fail is a gate nobody should trust.
    require(_members("protected abstract string ModeName { get; }",
                     r"\b(?:public|protected|internal)\s+abstract\s+[\w<>,\[\]\. ]+?\s+(?P<name>\w+)\s*[({=]")
            == {"ModeName"},
            "the abstract-member scanner no longer recognises an abstract property")
    require(_members("protected override string ModeName => \"X\";",
                     r"\b(?:public|protected|internal)\s+override\s+[\w<>,\[\]\. ]+?\s+(?P<name>\w+)\s*[({=]")
            == {"ModeName"},
            "the override scanner no longer recognises an expression-bodied override")

# ── ONE SUCCESSOR RULE, ONE EXPRESSION OF IT ────────────────────────────────────────────────
#
# A circuit's last leg runs from the last station back to the FIRST CIRCUIT station, not to
# _course[leg + 1] - so "what follows what" is BreakwaterCourseSettings.NextStation and nothing
# else. This shipped broken: the shoal loop was taught to walk every leg and its endpoint-
# clearance line twenty lines below kept a raw leg + 1, which indexed one past the end on the
# closing leg of EVERY build. It threw inside BuildEnvironment, so the whole arena - every
# station, not just the shoals - was lost, and the only symptom was a connecting screen that
# never released.
#
# Nothing else could have caught it: the C#-vs-model comparison compares station POSES, the unit
# tests exercise the station builder rather than the arena assembler, and the model computes its
# own shoals correctly. So the gate is a source rule, and it is the rule rather than the bug -
# any raw offset index into the course is refused.
_spawnable_rel = "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableBreakwater.cs"
_spawnable_src = read(_spawnable_rel) if exists(_spawnable_rel) else None


def raw_course_successors(src):
    """Indices into _course computed by arithmetic instead of through NextStation."""
    return [m.group(0) for m in re.finditer(r"_course\[[^\]]*[+\-][^\]]*\]", src or "")]


if _spawnable_src is not None:
    _raw = raw_course_successors(_spawnable_src)
    require(not _raw,
            "SpawnableBreakwater.cs indexes _course by arithmetic "
            f"({', '.join(sorted(set(_raw)))}). A course is a start gate plus a CIRCUIT, so the "
            "successor of the last station is BreakwaterCourseSettings.NextStation(leg, count), "
            "never leg + 1 - which runs off the end on the closing leg and takes the whole arena "
            "down with it.")

    # NEGATIVE CONTROL: a gate nobody has watched fail is a gate nobody should trust.
    require(raw_course_successors("float clear = EndClearance(_course[leg + 1]);"),
            "the raw-successor check no longer fires on the exact line that shipped broken")
    require(not raw_course_successors(
                "var to = _course[BreakwaterCourseSettings.NextStation(leg, _course.Count)];"),
            "the raw-successor check fires on the CORRECT form")

# ── THE MIRROR CHECK. The PhaseThresholds above are exact ONLY because breakwater_arena.py
#    reproduces the shipped C# geometry constant for constant. If one drifts, every threshold this
#    script authors describes an arena nobody flies - and nothing else in the project would notice,
#    because the model and the code are never compiled together.
_builder_cs = "Assets/_Scripts/Controller/Arcade/Breakwater/BreakwaterStationBuilder.cs"
_course_cs = "Assets/_Scripts/Controller/Arcade/Breakwater/BreakwaterCourse.cs"
_spawnable_cs = "Assets/_Scripts/Controller/Environment/MiniGameObjects/SpawnableBreakwater.cs"

for _rel, _name, _model_value, _label in [
    (_builder_cs, "RakePitch", arena.RAKE_PITCH, "plug rake pitch"),
    (_builder_cs, "RakeEdgeMargin", arena.RAKE_EDGE_MARGIN, "plug rake edge margin"),
    (_builder_cs, "MaxBarLength", arena.MAX_BAR_LEN, "longest plug bar"),
    (_builder_cs, "BarCross", arena.BAR_CROSS, "plug bar cross-section"),
    (_builder_cs, "CollarCount", arena.COLLAR_COUNT, "collar blocks"),
    (_builder_cs, "CollarCube", arena.COLLAR_CUBE, "collar block size"),
    (_builder_cs, "DishRatio", arena.DISH_RATIO, "dish radius / port radius"),
    (_builder_cs, "DishPitch", arena.DISH_PITCH, "dish ring + plate pitch"),
    (_builder_cs, "DishPlateWidth", arena.DISH_PLATE[0], "dish plate width"),
    (_builder_cs, "DishPlateThickness", arena.DISH_PLATE[2], "dish plate thickness"),
    (_builder_cs, "DishJitter", arena.DISH_JITTER, "dish plate jitter"),
    (_builder_cs, "DishHalfAngleDegrees", arena.DISH_HALF_ANGLE_DEG, "dish cone half-angle"),
    (_course_cs, "EyeRadius", arena.EYE_RADIUS, "threadable eye"),
    (_course_cs, "SparrowHullRadius", arena.SPARROW_HULL_RADIUS, "Sparrow hull radius"),
    (_course_cs, "DefaultInnerRadius", SHELL_INNER, "course shell, inner"),
    (_course_cs, "DefaultOuterRadius", SHELL_OUTER, "course shell, outer"),
    (_course_cs, "DefaultSpawnRingRadius", SPAWN_RING_RADIUS, "spawn ring radius"),
    (_course_cs, "AttemptsPerStation", arena.ATTEMPTS_PER_STATION, "walk attempts per station"),
    (_course_cs, "ReseedAttempts", arena.RESEED_ATTEMPTS, "reseeds before shortening"),
    (_spawnable_cs, "ShoalClustersPerLeg", arena.SHOAL_CLUSTERS_PER_LEG, "shoal clusters per leg"),
    (_spawnable_cs, "ShoalPrismsPerCluster", arena.SHOAL_PRISMS_PER_CLUSTER, "shoal prisms/cluster"),
    (_spawnable_cs, "ShoalCube", arena.SHOAL_CUBE, "shoal prism size"),
]:
    _got = cs_const(_rel, _name)
    if _got is not None and abs(_got - float(_model_value)) > 1e-6:
        errors.append(f"MODEL/CODE DRIFT ({_label}): {os.path.basename(_rel)}.{_name} = {_got} but "
                      f"breakwater_arena.py prices it at {_model_value}. Every PhaseThreshold this "
                      f"script authors is derived from the model, so they now describe a different "
                      f"arena than the one that ships.")

for _expr, _key, _field in (("return", "port", "PortRadiusForIntensity"),
                            ("float minStep", "min_step", "MinStep"),
                            ("MaxStep", "max_step", "MaxStep"),
                            ("MaxTurnDegrees", "max_turn", "MaxTurnDegrees"),
                            ("AxisJitterDegrees", "jitter", "AxisJitterDegrees"),
                            ("MaxPresentDegrees", "present", "MaxPresentDegrees")):
    _arr = cs_float_array(_course_cs, _expr)
    if _arr is None:
        continue
    _want = tuple(float(c[_key]) for c in arena.INTENSITIES)
    if _arr != _want:
        errors.append(f"MODEL/CODE DRIFT: BreakwaterCourseSettings {_field} = {_arr} but "
                      f"breakwater_arena.py sweeps {_want}")

# The prefab's authored shoal numbers and the C# field initializers must agree, or the class's own
# documented arithmetic (a clump radius at which no two cubes interpenetrate) stops describing the
# prefab that ships.
for _field, _authored in (("shoalOffsetMin", SHOAL["offset_min"]),
                          ("shoalOffsetMax", SHOAL["offset_max"]),
                          ("shoalStationClearance", SHOAL["station_clearance"]),
                          ("shoalSplineClearance", SHOAL["spline_clearance"]),
                          ("shoalClumpRadius", SHOAL["clump_radius"]),
                          ("shoalPlacementAttempts", SHOAL["placement_attempts"]),
                          ("shoalSpawnPadClearance", SHOAL["spawn_pad_clearance"])):
    _got = cs_const(_spawnable_cs, _field)
    if _got is not None and abs(_got - float(_authored)) > 1e-6:
        errors.append(f"prefab authors {_field} = {_authored} but SpawnableBreakwater.cs defaults "
                      f"it to {_got} - the inspector and the code would disagree")

# ── ONE PREFAB, NOT FOUR: prove the claim rather than asserting it in a comment ──────────────
_shoal_sets = {(arena.arena_totals(i)["shoal_prisms"], round(arena.arena_totals(i)["shoal_volume"]))
               for i in range(4)}
require(len(_shoal_sets) == 1,
        f"the shoals are NOT intensity-invariant ({sorted(_shoal_sets)}), so one shared "
        f"SpawnableBreakwater prefab can no longer serve all four intensities")

# ── The arena must fit the budgets the code declares ─────────────────────────────────────────
_lay_capacity = cs_const(_spawnable_cs, "LayCapacity")
if _lay_capacity is None:
    _m = re.search(r"LayCapacity\s*=>\s*(\d+)", read(_spawnable_cs))
    _lay_capacity = float(_m.group(1)) if _m else None
_biggest = max(arena.arena_totals(i)["prisms"] for i in range(4))
if _lay_capacity is not None:
    require(_biggest <= _lay_capacity,
            f"the widest arena is {_biggest} prisms but SpawnableBreakwater.LayCapacity is "
            f"{_lay_capacity:.0f}")

require(SENSE_RADIUS >= SHELL_OUTER + max(_DISH_REACH, _SHOAL_REACH),
        f"SenseRadiusOverride {SENSE_RADIUS} does not contain the arena "
        f"({SHELL_OUTER + max(_DISH_REACH, _SHOAL_REACH):.0f})")
require(SHELL_INNER < SPAWN_RING_RADIUS < SHELL_OUTER,
        f"the spawn ring ({SPAWN_RING_RADIUS}) is outside the course shell "
        f"({SHELL_INNER}..{SHELL_OUTER})")
require(SHELL_INNER < CRYSTAL_SPAWN_RADIUS < SHELL_OUTER,
        f"the crystal respawn radius ({CRYSTAL_SPAWN_RADIUS}) is outside the course shell")

# ── The phase ladder must sit ABOVE this arena's own mass at every intensity ─────────────────
for _i, (_cfg, _tot, _th) in enumerate(CELL_TOTALS, start=1):
    require(_th["RestlessEnterVolume"] > _tot["volume"],
            f"I{_i}: RestlessEnterVolume does not clear the arena's own volume - the cell would "
            f"boot pinned above Calm")
    require(_th["FrenzyEnterVolume"] > _th["RestlessEnterVolume"],
            f"I{_i}: FrenzyEnterVolume is not above RestlessEnterVolume")
    require(_th["RestlessExitVolume"] < _th["RestlessEnterVolume"]
            and _th["FrenzyExitVolume"] < _th["FrenzyEnterVolume"],
            f"I{_i}: an exit threshold is not below its enter (no hysteresis band)")
    require(_th["RestlessEnterCount"] > _tot["prisms"],
            f"I{_i}: the count backstop does not clear the arena's own prism count")

# ── The folded prose must RE-READ as the text that went in ──────────────────────────────────
#
# yaml_text enforces three rules about plain scalars by hand (no ": ", no " #", no leading "-").
# Hand-written rules about a serialization format are exactly the kind of thing that is right until
# the prose changes, so the rules are the floor and this is the proof: fold it, parse it back, and
# compare. One shipped cell Description (Boneyard's, "inside r=520: 4 hollow hulks") carries a ": "
# that only Unity's lenient reader forgives - a round-trip is what would have caught it.
try:
    import yaml as _yaml
except ImportError:
    notes.append("PyYAML is not installed, so the folded-prose round-trip did NOT run; only the "
                 "three structural plain-scalar rules were checked. pip install pyyaml to close it.")
else:
    for _key, _flat, _block in folded_blocks:
        try:
            _back = _yaml.safe_load("root:\n" + _block)["root"][_key]
        except Exception as exc:                                    # noqa: BLE001 - report anything
            errors.append(f"folded {_key} is not parseable YAML: {exc}")
            continue
        require(" ".join(str(_back).split()) == _flat,
                f"folded {_key} does not round-trip: YAML reads back "
                f"{' '.join(str(_back).split())[:80]!r}")

# ── GUID hygiene ─────────────────────────────────────────────────────────────────────────────
all_new = list(G_SCRIPT.values()) + list(G_ASSET.values())
require(len(set(all_new)) == len(all_new), "minted GUID collision within this script")

owned_metas = {os.path.normpath(os.path.join(ROOT, rel)) for rel in files if rel.endswith(".meta")}
existing_guids = set()
for dirpath, _, filenames in os.walk(os.path.join(ROOT, "Assets")):
    for fn in filenames:
        if not fn.endswith(".meta"):
            continue
        full = os.path.normpath(os.path.join(dirpath, fn))
        if full in owned_metas:
            continue
        try:
            with open(full, encoding="utf-8", errors="ignore") as fh:
                m = re.search(r"^guid: ([0-9a-f]{32})", fh.read(), re.M)
            if m:
                existing_guids.add(m.group(1))
        except OSError:
            pass

for g in all_new:
    if g in existing_guids:
        errors.append(f"minted GUID {g} collides with an asset this script does not own")
for name, g in EXISTING.items():
    if g not in existing_guids:
        errors.append(f"referenced GUID for {name} ({g}) does not resolve to any asset")

# Racing/ and RaceGateRing belong to author_switchback_assets.py and are committed. Emitting them
# here with a re-derived guid would dangle every serialized reference to the shared ring class, so
# assert we do NOT own them and that they still carry the guids we expect to point at.
for rel, want in RACING_OWNED_ELSEWHERE.items():
    require(rel not in files, f"{rel} is owned by author_switchback_assets.py - do not emit it here")
    if exists(rel):
        m = re.search(r"^guid: ([0-9a-f]{32})", read(rel), re.M)
        require(m is not None and m.group(1) == want,
                f"{rel} no longer carries {want} - RaceGateRing's references would be dangling")
    else:
        errors.append(f"{rel} is missing - run author_switchback_assets.py first")

for _k, _p in SCRIPT_PATHS.items():
    require(exists(_p), f"script {_p} does not exist")
# A .meta for a file that is not there is an orphan Unity will delete on the next import, so the
# doc's absence is reported rather than papered over.
require(exists(BREAKWATER_DOC),
        f"{BREAKWATER_DOC} does not exist, but this script authors its .meta - write the doc or "
        f"drop the meta")

# ── Key validation: every top-level key must be a field on the script(s) that own the asset ──
#
# NO hand-written whitelist. The older generators carried an SO_BASE set listing PreviewClip, which
# is exactly how a retired key survived a key check - the check answered "is this in my list?"
# rather than "is this a field?".
def cs_fields(path: str) -> "set[str]":
    src = read(path)
    out = set()
    TYPE = r"[\w<>,\[\]\?\.]+"
    MODS = (r"(?:(?:public|protected|private|internal|static|const|readonly|new|virtual|"
            r"override|abstract|sealed|partial)\s+)+")
    for m in re.finditer(MODS + TYPE + r"\s+(\w+)\s*(?:=|;|\{|=>|\()", src):
        out.add(m.group(1))
    for m in re.finditer(r"\[SerializeField[^\]]*\]\s*(?:\[[^\]]*\]\s*)*"
                         r"(?:(?:public|protected|private|internal)\s+)?"
                         + TYPE + r"\s+(\w+)\s*(?:=|;)", src):
        out.add(m.group(1))
    for m in re.finditer(r"^\s{4,}" + TYPE + r"\s+(\w+)\s*\{\s*get;", src, re.M):
        out.add(m.group(1))
    return out


UNITY_MB_KEYS = {"m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance",
                 "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script",
                 "m_Name", "m_EditorClassIdentifier"}

KEY_CHECKS = [
    ("Assets/_SO_Assets/Games/ArcadeGameBreakwater.asset",
     ["Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs",
      "Assets/_Scripts/ScriptableObjects/SO_Game.cs"]),
    ("Assets/_SO_Assets/Scoring Rules/BreakwaterScoringRule.asset",
     ["Assets/_Scripts/Controller/Arcade/Scoring/GateRaceScoringRuleSO.cs",
      "Assets/_Scripts/Controller/Arcade/Scoring/ScoringRuleSO.cs"]),
    (f"{CELL_DIR}/Breakwater Spawn Profile.asset",
     ["Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs"]),
    ("Assets/_Prefabs/Spawnables/SpawnableBreakwater.prefab",
     [_spawnable_cs,
      "Assets/_Scripts/Controller/Environment/Spawning/CellEnvironmentSpawnableBase.cs",
      "Assets/_Scripts/Controller/Environment/Spawning/SpawnableBase.cs"]),
]
for _i in (1, 2, 3, 4):
    KEY_CHECKS.append((f"{CELL_DIR}/Breakwater Cell Config {_i}.asset",
                       ["Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs"]))

for asset_path, cs_paths in KEY_CHECKS:
    body = files[asset_path]
    mb = body[body.index("MonoBehaviour:"):] if "MonoBehaviour:" in body else body
    keys = set(re.findall(r"^  (\w+):", mb, re.M)) - UNITY_MB_KEYS
    known: "set[str]" = set()
    for cs in cs_paths:
        if exists(cs):
            known |= cs_fields(cs)
        else:
            errors.append(f"{cs} not found - cannot validate {os.path.basename(asset_path)}")
    unknown = keys - known
    if unknown:
        errors.append(f"{os.path.basename(asset_path)}: keys not found on its script(s): "
                      f"{sorted(unknown)} - a key Unity cannot bind is silently dropped on the "
                      f"first re-save, so authoring one is authoring nothing")

# The SpawnProfile must author EVERY field, because Unity fills a missing key with the TYPE
# default rather than the field initializer.
_profile_keys = set(re.findall(r"^  (\w+):",
                               files[f"{CELL_DIR}/Breakwater Spawn Profile.asset"], re.M)) \
    - UNITY_MB_KEYS
_profile_declared = set(re.findall(
    r"public\s+(?:bool|int|float|List<\w+>)\s+(\w+)\s*(?:=|;)",
    read("Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs")))
_missing = _profile_declared - _profile_keys
require(not _missing,
        f"Breakwater Spawn Profile omits {sorted(_missing)} - Unity fills a MISSING key with the "
        f"TYPE default, not the field initializer, so an omitted scale deserializes as 0 and an "
        f"omitted InitialFaunaReleaseTier seals the biome")

# ── The card: single vessel, and the retired keys really are gone ────────────────────────────
_card = files["Assets/_SO_Assets/Games/ArcadeGameBreakwater.asset"]
_vessels = re.search(r"^  Vessels:\n((?:  - .*\n)*)", _card, re.M)
if require(_vessels is not None, "ArcadeGameBreakwater has no Vessels block"):
    require(_vessels.group(1).count("- {fileID") == 1,
            "ArcadeGameBreakwater must author EXACTLY ONE vessel (Sparrow)")
    require(EXISTING["Vessel_Sparrow"] in _vessels.group(1),
            "ArcadeGameBreakwater's single vessel is not the Sparrow")
for _retired in ("CallToActionTargetType", "PreviewClip"):
    require(f"  {_retired}:" not in _card,
            f"ArcadeGameBreakwater authors the RETIRED key {_retired}")
require(f"  Mode: {MODE_BREAKWATER}\n" in _card, "the card does not carry GameModes.Breakwater")
require(MODE_BREAKWATER not in (7, 31),
        f"GameModes.Breakwater = {MODE_BREAKWATER} reuses a permanently reserved id "
        f"(7 = retired Freestyle, 31 = never assigned)")
require("  GolfScoring: 1\n" in _card,
        "Breakwater is golf-scored (finish time vs sentinel) and must match the rule asset")
require("  MinDomainsAllowed: 2\n" in _card,
        "a race needs a rival - one domain reaches the objective with nobody to have beaten")

# ── The scene: whatever produced it, it must still be wired for THIS mode ────────────────────
#
# This is the block that carries the weight when the scene is registered read-only: the byte diff
# is vacuous there by construction, so these are the checks that actually gate the shipped file.
sc = files[SCENE_PATH]
for _name in ("SalvoController", "SalvoPrismTurnMonitor", "SalvoScoringRule"):
    require(EXISTING[_name] not in sc, f"the scene still references {_name}")
require(G_SCRIPT["BreakwaterController"] in sc, "the scene is missing BreakwaterController")
# The SHARED monitor, not a Breakwater one: this mode is a GateRaceController like Switchback and
# Headlong, so its turn monitor is the platform's. A scene still naming a mode-local monitor is a
# scene that never made the adoption.
require(EXISTING["RaceGateTurnMonitor"] in sc,
        "the scene is missing RaceGateTurnMonitor - Breakwater is a GateRaceController and shares "
        "the platform's monitor")
for _retired in ("BreakwaterStationTurnMonitor", "BreakwaterObjectiveProvider"):
    require(_retired not in sc, f"the scene still names the retired {_retired}")
require(G_ASSET["BreakwaterScoringRule"] in sc, "the scene is missing the scoring rule reference")
require(G_ASSET["SpawnableBreakwater.prefab"] in sc,
        "the scene does not point the controller at the arena prefab - every port would be an "
        "open hoop and the mode's whole fire/saw/thread choice would be missing")
for _name in ("BoneyardCell1", "BoneyardCell2", "BoneyardCell3", "BoneyardCell4"):
    require(EXISTING[_name] not in sc, f"the scene still references {_name} - the Boneyard survived")
for _i in (1, 2, 3, 4):
    require(G_ASSET[f"BreakwaterCellConfig{_i}"] in sc,
            f"the scene is missing Breakwater Cell Config {_i}")
require("  cellTypeChoiceOptions: 1\n" in sc,
        "the scene does not select its cell IntensityWise - the port radius moves the arena's "
        "mass, so each intensity needs its own measured PhaseThresholds")
require("  spawnFormation: 1\n" in sc,
        "the scene is not on the equatorial spawn formation - station 1 sits on that ring's POLE, "
        "and under a tetrahedral spread no point is equidistant from every pilot")
require("  arrangeSpawnPointsAroundCell: 1\n" in sc, "the scene lost the cell-relative spawn ring")
require(f"  spawnRingRadiusFloor: {int(SPAWN_RING_RADIUS)}\n" in sc,
        f"the scene's spawn ring floor is not {int(SPAWN_RING_RADIUS)} - this cell has no nucleus, "
        f"so ExpectedNucleusWorldRadius is 0 and the ring would collapse onto the cell centre")
require(f"  noNucleusSpawnRadius: {CRYSTAL_SPAWN_RADIUS}\n" in sc,
        "the scene does not author noNucleusSpawnRadius - with no nucleus every omni crystal falls "
        "through to its own SphereRadius and stacks on the arena's exact centre")
require(f"  differenceSource: {COMEBACK_SWITCHES_THREADED}\n" in sc,
        "the scene's comeback source is not SwitchesThreaded - a scene-authored "
        "ElementalComebackSystem is used AS AUTHORED, so DefaultSourceFor never runs")
require("  useGolfRules: 1\n" in sc, "the scene did not take the golf-rules flag")
require(sc.count("  - vesselClass: 11\n") == 4,
        "the scene does not carry 4 Sparrow AI templates")
# The Cell's scene fileID is referenced by its GameObject's component list and by two sibling
# components; a clone that renumbered it would strand all of them.
require(f"--- !u!114 &{CELL_SCENE_FILEID}\n" in sc
        and sc.count(f"{{fileID: {CELL_SCENE_FILEID}}}") >= 3,
        f"the Cell's scene fileID {CELL_SCENE_FILEID} did not survive - its component references "
        f"would be dangling")
# The controller's shell must equal the model's, or the 400-seed sweep proves a course nobody flies.
for _field, _want in (("courseOuterRadius", SHELL_OUTER),
                      ("courseInnerRadiusFallback", SHELL_INNER)):
    require(f"  {_field}: {num(_want)}\n" in sc,
            f"the scene's {_field} is not {num(_want)} - it would describe a different shell than "
            f"the one BreakwaterCourseTests sweeps and breakwater_arena.py prices")

# ── Registration sanity ──────────────────────────────────────────────────────────────────────
require(f"    guid: {G_ASSET['MinigameBreakwater.unity']}\n" in files[BUILD_PATH],
        "the scene is not in EditorBuildSettings - launching the card would fail")
require(re.search(rf"^  - {MODE_BREAKWATER}$", files[PROG_PATH], re.M) is not None,
        "GameModes.Breakwater is not in alwaysUnlockedModes")
for _key in ("breakwaterStationTarget", "breakwaterStationTargetBuild"):
    require(f"  {_key}: {STATION_TARGET}\n" in files[END_PATH],
            f"{_key} is not {STATION_TARGET} in EndConditionOverrides.asset")
for _key in ("breakwaterLaps", "breakwaterLapsBuild"):
    require(f"  {_key}: {LAPS}\n" in files[END_PATH],
            f"{_key} is not {LAPS} in EndConditionOverrides.asset")
    require(re.search(rf"public int {_key}\b", read(_endcond_cs)) is not None,
            f"EndConditionOverridesSO has no field {_key}")

# Explicitly NOT ours - assert we left them alone rather than trusting a comment.
for _shared in ("Assets/Resources/ObjectiveIconSet.asset",
                "Assets/Resources/ModeControlsLibrary.asset"):
    require(_shared not in files,
            f"{_shared} is keyed on the METRIC and already carries a row for "
            f"{METRIC_SWITCHES_THREADED}; two generators SETting one row flip it on alternate runs")


# ══════════════════ REPORT, THEN WRITE ═══════════════════════════════════════════════════════
if errors:
    print("VALIDATION FAILED - nothing written:")
    for e in errors:
        print("  x", e)
    sys.exit(1)

print(f"Validation passed ({len(files)} files).")
print(f"  scene: {scene_source}")
print(f"  stations {STATION_TARGET} x {LAPS} laps = {CROSSING_TARGET} crossings  "
      f"comeback {COMEBACK_RATE} "
      f"({_quarter:.2f} levels at a quarter-of-target deficit)  sense radius {SENSE_RADIUS}")
for _i, (_cfg, _tot, _th) in enumerate(CELL_TOTALS, start=1):
    print(f"  I{_i}: port {_cfg['port']:.0f}  {_tot['prisms']:>6,} prisms  "
          f"{_tot['volume']:>10,.0f} volume  ->  Restless {_th['RestlessEnterVolume']:>10,.0f}  "
          f"Frenzy {_th['FrenzyEnterVolume']:>10,.0f}")
for n in notes:
    print(f"  NOTE: {n}")

if CHECK_ONLY:
    # A generator's --check is only a gate if it re-reads what SHIPPED: validating this script's own
    # constants proves the script is self-consistent and says nothing about the assets. So diff
    # every generated file against disk, normalising CRLF (a Windows checkout is not drift).
    drift = []
    for rel, content in sorted(files.items()):
        path = os.path.join(ROOT, rel)
        if not os.path.exists(path):
            drift.append((rel, "missing on disk"))
            continue
        with open(path, "r", encoding="utf-8", newline="") as fh:
            on_disk = fh.read().replace("\r\n", "\n")
        if on_disk != content:
            a, b = on_disk.split("\n"), content.split("\n")
            where = next((i for i in range(max(len(a), len(b)))
                          if (a[i] if i < len(a) else None) != (b[i] if i < len(b) else None)), 0)
            drift.append((rel, f"first differs at line {where + 1}: disk {a[where]!r} != "
                               f"authored {b[where]!r}"
                          if where < len(a) and where < len(b) else
                          f"length differs ({len(a)} lines on disk, {len(b)} authored)"))
    if drift:
        print("\n--check FAILED - shipped assets have drifted from what this script authors:")
        for rel, why in drift:
            print("  x", rel)
            print("     ", why)
        print("\nRe-run without --check to re-author, or fix this script if the drift is intended.")
        sys.exit(1)
    print(f"\n--check: no files written; all {len(files)} files match what this script authors.")
    sys.exit(0)

for rel, content in files.items():
    path = os.path.join(ROOT, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(content)
print(f"\nWrote {len(files)} files.")
for rel in sorted(files):
    print("  ", rel)
