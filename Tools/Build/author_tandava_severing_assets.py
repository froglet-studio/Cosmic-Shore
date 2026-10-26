#!/usr/bin/env python3
"""
Author every serialized asset TANDAVA: SEVERING (GameModes.TandavaSevering, read from the enum) adds - Voyage 2, the fork
of Tandava that carries PR #1046's severing, wound memory and whale-jelly chimera.
Assets/_Scripts/Controller/Arcade/TandavaSevering/TANDAVA_SEVERING.md is the design and status doc.

WHY A FORK. Tandava (GameModes.Tandava, Voyage 1, author_tandava_assets.py) is Shombith's and still moving. The severing
work was built on an older Tandava and is not part of it; Garrett (2026-10-26) asked for it "contained on a second voyage
card in the voyages" so it never collides with Tandava's own development. So this mode has its own code
(Assets/_Scripts/Controller/Arcade/TandavaSevering/, every type prefixed TandavaSevering), its own scene, card, settings,
swarm and cell, and reads Tandava's files without ever writing one.

HOW. Every asset here is DERIVED from Tandava's committed asset of the same role: read it, swap the guids that make it
the fork's, add what only the fork has. So a tuning pass on Tandava (its numbers, its reef, its plans' armour) reaches
the fork the next time this script runs, and `--check` goes red the moment Tandava moves - the cue to re-run, look at
the diff, and keep or drop what came across. The ladder's 51 plans are referenced by Tandava's own guids; the fork adds
four of its own (tandava_severing_plans.py: the Severed and its strike twin, the chimera's two shapes), appended so
every ladder index stays put.

Owned here (and nowhere else):
  * Assets/_SO_Assets/Swarm Fauna/TandavaSevering/ (the four plans, the swarm config),
    Assets/_Prefabs/FloraAndFauna/TandavaSeveringSwarmFauna.prefab,
    Assets/_SO_Assets/Cell Configs/Tandava Severing Cell/ (the species, the spawn profile, the cell config);
  * the mode: TandavaSeveringSettings.asset, TandavaSeveringScoringRule.asset, the card (parked in Games/_Archive like
    Tandava's: a voyage is on the master roster and on no grid), the toasts, the preview, MinigameTandavaSevering.unity;
  * the .meta of every script in Assets/_Scripts/Controller/Arcade/TandavaSevering/ and of TANDAVA_SEVERING.md.
Registered in the shared registries: the master roster, always-unlocked, the build list (after MinigameTandava), the
toast library, the preview library.

Run:  python3 Tools/Build/author_tandava_severing_assets.py [--check]
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib            # noqa: E402
import tandava_plans as tp               # noqa: E402
import tandava_severing_plans as sp      # noqa: E402

ROOT = lib.ROOT
guid = lib.guid
num = lib.num
GAME_MODES_CS = "Assets/_Scripts/Data/Enums/GameModes.cs"
TOASTS_CS = "Assets/_Scripts/Data/Enums/GameToastSituation.cs"


def enum_value(enum_file, member):
    m = re.search(rf"\b{member}\s*=\s*(\d+)\s*,", open(os.path.join(ROOT, enum_file), encoding="utf-8").read())
    assert m, f"{member} is not in {enum_file}"
    return int(m.group(1))


MODE_ID = enum_value(GAME_MODES_CS, "TandavaSevering")
TANDAVA_ID = enum_value(GAME_MODES_CS, "Tandava")
FORK_TOASTS = [enum_value(TOASTS_CS, f"Tandava{k}") for k in ("Severed", "Rejoined", "Succession", "Learned", "Chimera")]
g = lib.Generator(MODE_ID, "TandavaSevering")
errors = []

# ── Tandava's files: read, never written ─────────────────────────────────────
T = dict(
    swarm="Assets/_SO_Assets/Swarm Fauna/Tandava/TandavaSwarmFaunaConfig.asset",
    prefab="Assets/_Prefabs/FloraAndFauna/TandavaSwarmFauna.prefab",
    species="Assets/_SO_Assets/Cell Configs/Tandava Cell/Tandava Swarm Fauna Config Data.asset",
    profile="Assets/_SO_Assets/Cell Configs/Tandava Cell/Tandava Cell Spawn Profile.asset",
    cell="Assets/_SO_Assets/Cell Configs/Tandava Cell/Tandava Cell Config.asset",
    settings="Assets/_SO_Assets/Games/TandavaSettings.asset",
    rule="Assets/_SO_Assets/Scoring Rules/TandavaScoringRule.asset",
    card="Assets/_SO_Assets/Games/_Archive/ArcadeGameTandava.asset",
    toasts="Assets/_SO_Assets/Game Toasts/GameToastConfig_Tandava.asset",
    preview="Assets/_SO_Assets/Mode Previews/ModePreview_Tandava.asset",
    scene=f"{lib.SCENES_DIR}/MinigameTandava.unity",
)
TG = {k: lib.existing_guid(p) for k, p in T.items()}
TS = {k: lib.existing_guid(f"Assets/_Scripts/Controller/Arcade/{p}.cs") for k, p in (
    ("controller", "Tandava/TandavaController"), ("settings", "Tandava/TandavaSettingsSO"),
    ("rule", "Scoring/TandavaScoringRuleSO"), ("monitor", "TurnMonitors/TandavaTurnMonitor"))}

# ── The fork's files ─────────────────────────────────────────────────────────
CODE_DIR = "Assets/_Scripts/Controller/Arcade/TandavaSevering"
SWARM_DIR = "Assets/_SO_Assets/Swarm Fauna/TandavaSevering"
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Tandava Severing Cell"
SCRIPTS = ["TandavaSeveringController", "TandavaSeveringDirectorCore", "TandavaSeveringSettingsSO", "TandavaSeveringSever",
           "TandavaSeveringObjectiveProvider", "TandavaSeveringScoringRuleSO", "TandavaSeveringTurnMonitor"]
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPTS}
P = dict(
    swarm=f"{SWARM_DIR}/TandavaSeveringSwarmFaunaConfig.asset",
    prefab="Assets/_Prefabs/FloraAndFauna/TandavaSeveringSwarmFauna.prefab",
    species=f"{CELL_DIR}/Tandava Severing Swarm Fauna Config Data.asset",
    profile=f"{CELL_DIR}/Tandava Severing Cell Spawn Profile.asset",
    cell=f"{CELL_DIR}/Tandava Severing Cell Config.asset",
    settings="Assets/_SO_Assets/Games/TandavaSeveringSettings.asset",
    rule="Assets/_SO_Assets/Scoring Rules/TandavaSeveringScoringRule.asset",
    card="Assets/_SO_Assets/Games/_Archive/ArcadeGameTandavaSevering.asset",
    toasts="Assets/_SO_Assets/Game Toasts/GameToastConfig_TandavaSevering.asset",
    preview="Assets/_SO_Assets/Mode Previews/ModePreview_TandavaSevering.asset",
)
SCENE_NAME = "MinigameTandavaSevering"
G = {k: guid(f"asset/TandavaSevering/{k}") for k in list(P) + ["scene"]}
NAME = {k: os.path.splitext(os.path.basename(p))[0] for k, p in P.items()}


def swap(text, old, new, label, expected=1):
    return lib.swap_guid(text, old, new, label, expected)


def rename(text, old, new, label):
    return lib.replace_block(text, f"  m_Name: {old}\n", f"  m_Name: {new}\n", label)


def count_of(text, needle):
    return len(re.findall(re.escape(needle), text))


# ═════════════════════════════════════════════════════════════════════════════
# 0. Metas
# ═════════════════════════════════════════════════════════════════════════════
for d in (CODE_DIR, SWARM_DIR, CELL_DIR):
    g.folder_meta(d)
g.text_meta(f"{CODE_DIR}/TANDAVA_SEVERING.md")
for k in SCRIPTS:
    g.script_meta(f"{CODE_DIR}/{k}.cs", G_SCRIPT[k])
# the controller's partial (no MonoBehaviour of its own, but Unity still wants the meta)
g.script_meta(f"{CODE_DIR}/TandavaSeveringController.Severing.cs", guid("script/TandavaSeveringController.Severing"))

TEXT_META = ("fileFormatVersion: 2\nguid: {}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n"
             "  assetBundleName: \n  assetBundleVariant: \n")

# ═════════════════════════════════════════════════════════════════════════════
# 1. The fork's four plans
# ═════════════════════════════════════════════════════════════════════════════
PLANS = sp.bake_all()
errors += [f"plans: {e}" for e in sp.validate(PLANS)]
FORK_KEYS = sp.plan_keys()
LADDER_KEYS = sp.LADDER_KEYS
G_PLAN = {k: guid(f"asset/TandavaSeveringPlan_{k}") for k in FORK_KEYS}
for k in FORK_KEYS:
    rel = f"{SWARM_DIR}/SwarmPlan_tandava_{k}.json"
    g.emit(rel, tp.dumps(PLANS[k]))
    g.emit(rel + ".meta", TEXT_META.format(G_PLAN[k]))
IX = {k: len(LADDER_KEYS) + i for i, k in enumerate(FORK_KEYS)}   # the fork's plans' indices in its ScriptedPlans

# ═════════════════════════════════════════════════════════════════════════════
# 2. The swarm: Tandava's config plus the four plans; its prefab, species, spawn profile and cell
# ═════════════════════════════════════════════════════════════════════════════
swarm = rename(g.read(T["swarm"]), "TandavaSwarmFaunaConfig", NAME["swarm"], "swarm config")


def append_list(text, key, entries, want_len):
    m = re.search(rf"(?m)^  {key}:\n((?:  - [^\n]*\n)+)", text)
    assert m, f"swarm config: {key} not found"
    have = len(m.group(1).splitlines())
    if have != want_len:
        errors.append(f"Tandava's swarm config lists {have} {key}, its plan catalogue {want_len} - "
                      "tandava_plans and the committed config disagree (re-run author_tandava_assets.py?)")
    return text.replace(m.group(0), m.group(0) + "".join(f"  - {e}\n" for e in entries), 1)


swarm = append_list(swarm, "ScriptedPlans", [f"{{fileID: 4900000, guid: {G_PLAN[k]}, type: 3}}" for k in FORK_KEYS],
                    len(LADDER_KEYS))
swarm = append_list(swarm, "ScriptedPlanPeriods", [str(PLANS[k]["frameSteps"]) for k in FORK_KEYS], len(LADDER_KEYS))
swarm = append_list(swarm, "ScriptedPlanWellClip", [num(PLANS[k].get("wellClip", 1)) for k in FORK_KEYS], len(LADDER_KEYS))
g.emit_asset(P["swarm"], G["swarm"], swarm)
DENSITY = int(re.search(r"(?m)^  PlanDensity: (\d+)$", swarm).group(1))
UNIT_SCALE = float(re.search(r"(?m)^  UnitScale: ([\d.]+)$", swarm).group(1))
WORLD = DENSITY ** (1.0 / 3.0) * UNIT_SCALE   # plan voxels -> world (author_tandava_assets.WORLD)

prefab = swap(g.read(T["prefab"]), TG["swarm"], G["swarm"], "prefab's swarm config")
prefab = rename(prefab, "TandavaSwarmFauna", "TandavaSeveringSwarmFauna", "prefab")
g.emit_prefab(P["prefab"], G["prefab"], prefab)

species = swap(g.read(T["species"]), TG["prefab"], G["prefab"], "species' prefab")
g.emit_asset(P["species"], G["species"], rename(species, "Tandava Swarm Fauna Config Data", NAME["species"], "species"))

profile = swap(g.read(T["profile"]), TG["species"], G["species"], "spawn profile's species")
g.emit_asset(P["profile"], G["profile"], rename(profile, "Tandava Cell Spawn Profile", NAME["profile"], "spawn profile"))

cell = swap(g.read(T["cell"]), TG["profile"], G["profile"], "cell's spawn profile")
cell = lib.replace_block(cell, "  CellName: Tandava\n", "  CellName: Tandava Severing\n", "cell name")
g.emit_asset(P["cell"], G["cell"], rename(cell, "Tandava Cell Config", NAME["cell"], "cell config"))

# ═════════════════════════════════════════════════════════════════════════════
# 3. The settings: Tandava's, plus the chimera on the Many-Headed Serpent, the Severed, the new dials, lines and labels
# ═════════════════════════════════════════════════════════════════════════════
FORK_SRC = {k: g.read(f"{CODE_DIR}/{k}.cs") for k in SCRIPTS}


def director_fields(src, cls):
    """(name, default text, kind) of every serialized public float/int field of `cls`, in declaration order."""
    body = src.split(f"public sealed class {cls}")[1].split("\n    }\n")[0]
    out = []
    for m in re.finditer(r"(?m)^\s*(\[NonSerialized\]\s*)?public (float|int) ([^;]+);", body):
        if m.group(1):
            continue
        for decl in m.group(3).split(","):
            name, value = (x.strip() for x in decl.split("="))
            out.append((name, value.rstrip("f"), m.group(2)))
    return out


s = g.read(T["settings"])
s = swap(s, TS["settings"], G_SCRIPT["TandavaSeveringSettingsSO"], "settings script")
s = swap(s, TG["swarm"], G["swarm"], "settings' swarm config")
s = rename(s, "TandavaSettings", NAME["settings"], "settings")

# the chimera: every variant names it (0 = none); only the Many-Headed Serpent's (form 1) pass through it
lines, form, out = s.split("\n"), -1, []
for ln in lines:
    if re.match(r"^  - DisplayName: ", ln):
        form += 1
    out.append(ln)
    if re.match(r"^      HaloCentre: ", ln):
        w, j = (IX["chimera_whale"], IX["chimera_jelly"]) if form == 1 else (0, 0)
        out += [f"      ChimeraPlanIndex: {w}", f"      ChimeraAltPlanIndex: {j}"]
s = "\n".join(out)
if form != 3:
    errors.append(f"Tandava's settings carry {form + 1} forms, not four")

form0_meal = float(re.search(r"(?m)^    MealVolume: ([\d.]+)$", s).group(1))


def v3(p):
    return "{x: %s, y: %s, z: %s}" % tuple(num(round(float(x) * WORLD, 3)) for x in p)


SEVERED_YAML = (
    "  SeveredForm:\n    DisplayName: the Severed\n    Role: 3\n    FillToEvolve: 0.9\n    BankShare: 0\n"
    f"    MealVolume: {num(0.5 * form0_meal)}\n    Line: \n    Variants:\n"
    f"    - DisplayName: the Severed\n      PlanIndex: {IX['severed']}\n      FeedPlanIndex: {IX['severed_feed']}\n"
    "      CoilPlanIndices: []\n      CoilMouths: []\n      CoilRoamRadius: 0\n"
    "      LungePlanIndex: -1\n      SnapPlanIndex: -1\n      LungeMouth: {x: 0, y: 0, z: 0}\n"
    f"      Mouth: {v3(PLANS['severed']['mouth'])}\n      FeedMouth: {v3(PLANS['severed_feed']['mouth'])}\n"
    "      HaloCentre: {x: 0, y: 0, z: 0}\n      ChimeraPlanIndex: 0\n      ChimeraAltPlanIndex: 0\n"
    f"  SwarmSpawn: {{fileID: 11400000, guid: {G['species']}, type: 2}}\n")

# the director: Tandava's tuned values wherever Tandava has the dial, the fork's C# default for the dials only it has
m = re.search(r"(?m)^  Director:\n((?:    [^\n]*\n)+)", s)
assert m, "settings: Director block not found"
tandava_dials = dict(re.findall(r"(?m)^    (\w+): ([^\n]*)$", m.group(1)))
fork_dials = director_fields(FORK_SRC["TandavaSeveringDirectorCore"], "TandavaSeveringDirectorSettings")
missing = [n for n in tandava_dials if n not in {f[0] for f in fork_dials}]
if missing:
    errors.append(f"Tandava's director has dials the fork's does not: {missing} (port them to TandavaSeveringDirectorCore)")
director = "  Director:\n" + "".join(
    f"    {n}: {tandava_dials[n] if n in tandava_dials else (num(float(v)) if k == 'float' else str(int(float(v))))}\n"
    for n, v, k in fork_dials)
s = s.replace(m.group(0), SEVERED_YAML + director, 1)


def ys(text):
    return f"'{text}'" if re.search(r"[:#'\"{}\[\],&*!|>%@`]", text) else text


LINES = [("SeveredLine", "You cut it in two. The severed half crawls off to feed - stop it getting home."),
         ("RejoinedLine", "The severed half found its way home. It is whole again."),
         ("SuccessionLine", "You cut the body away - but the severed half lives, and it remembers the shape."),
         ("ChimeraLine", "It is tearing between shapes - a whale, a jelly, neither. Strike it as it turns."),
         ("LearnedFeedingLine", "It remembers being struck at the table. It eats far from you now, and bolts sooner."),
         ("LearnedLungingLine", "It remembers what its lunges cost. It will not turn on you so readily."),
         ("LearnedChasedLine", "It remembers being run down. It feels you coming from farther away."),
         ("LearnedSeveredLine", "It remembers being cut in two. Its severed halves hurry home.")]
LABELS = [("ChimeraName", "Whale-Jelly Chimera"), ("ChimeraLabel", "Unstable"),
          ("ChimeraTurningLabel", "Turning - it cannot heal"), ("SeveredLabel", "The Severed"),
          ("SeveredOutFormat", "home in {0}"), ("SeveredHomeLabel", "crawling home - cut it off")]
for after, rows in (("LungeLine", LINES), ("BodyFormat", LABELS)):
    m = re.search(rf"(?m)^  {after}: [^\n]*\n", s)
    assert m, f"settings: {after} not found"
    s = s.replace(m.group(0), m.group(0) + "".join(f"  {k}: {ys(v)}\n" for k, v in rows), 1)
for k, v in LINES + LABELS:   # the C# default and the asset say the same (the SO's own fallback text)
    if f'{k} = "{v}"' not in FORK_SRC["TandavaSeveringSettingsSO"]:
        errors.append(f"TandavaSeveringSettingsSO.{k}'s default is not the authored {v!r}")
g.emit_asset(P["settings"], G["settings"], s)

# ═════════════════════════════════════════════════════════════════════════════
# 4. The mode: rule, toasts, preview, card
# ═════════════════════════════════════════════════════════════════════════════
rule = swap(g.read(T["rule"]), TS["rule"], G_SCRIPT["TandavaSeveringScoringRuleSO"], "rule script")
g.emit_asset(P["rule"], G["rule"], rename(rule, "TandavaScoringRule", NAME["rule"], "rule"))

toasts = rename(g.read(T["toasts"]), "GameToastConfig_Tandava", NAME["toasts"], "toasts")
toasts = lib.replace_block(toasts, f"  gameMode: {TANDAVA_ID}\n", f"  gameMode: {MODE_ID}\n", "toasts' mode")
toasts += "".join(lib.toast(t, "{0}", domain_names=0) for t in FORK_TOASTS)
g.emit_asset(P["toasts"], G["toasts"], toasts)
g.register_toast_config(G["toasts"])

preview = rename(g.read(T["preview"]), "ModePreview_Tandava", NAME["preview"], "preview")
preview = lib.replace_block(preview, f"  Mode: {TANDAVA_ID}\n", f"  Mode: {MODE_ID}\n", "preview's mode")
preview = swap(preview, TG["cell"], G["cell"], "preview cell")
preview = preview.replace("'The Tandava cell as", "'The Tandava Severing cell (Tandava''s, forked) as", 1)
g.emit_asset(P["preview"], G["preview"], preview)
g.register_preview(G["preview"])

card = rename(g.read(T["card"]), "ArcadeGameTandava", NAME["card"], "card")
card = lib.replace_block(card, f"  Mode: {TANDAVA_ID}\n", f"  Mode: {MODE_ID}\n", "card's mode")
card = lib.replace_block(card, "  DisplayName: Tandava\n", "  DisplayName: 'Tandava: Severing'\n", "card's name")
card = lib.replace_block(card, "  SceneName: MinigameTandava\n", f"  SceneName: {SCENE_NAME}\n", "card's scene")
DESCRIPTION = ("The hunt from Tandava, and a creature that fights back. Cut clean through its body and the piece crawls "
               "off as the Severed, a second serpent that feeds and races home to rejoin it. It remembers every wound, "
               "and the play that hurt it once works less the next time. On its way up to the dance it tears between a "
               "whale and a jelly. Shatter it or break its halo before its last feast. Everyone flies together.")
m = re.search(r"(?m)^  Description: [^\n]*\n(?:    [^\n]*\n)*", card)
assert m, "card: Description not found"
card = card.replace(m.group(0), "  Description: " + lib.wrap_yaml_scalar(DESCRIPTION) + "\n", 1)
TIPS = ["A cut clean through its middle parts it in two. Kill the Severed before it gets home, or it brings its members back.",
        "A starved creature cut in two stays cut. A fed one regrows both halves - starve it first.",
        "It learns. Break its meals every time and it eats farther from you; run it down and it feels you sooner.",
        "As the whale-jelly chimera it cannot heal while it turns, and it shatters sooner. Finish it mid-turn."]
m = re.search(r"(?m)^  Tips:\n((?:  - [^\n]*\n)+)", card)
assert m, "card: Tips not found"
card = card.replace(m.group(0), m.group(0) + "".join(f"  - {ys(t)}\n" for t in TIPS), 1)
g.emit_asset(P["card"], G["card"], card)

# ═════════════════════════════════════════════════════════════════════════════
# 5. The scene: Tandava's, wired to the fork
# ═════════════════════════════════════════════════════════════════════════════
scene = g.read(T["scene"])
for old, new, label in ((TS["controller"], G_SCRIPT["TandavaSeveringController"], "controller"),
                        (TS["monitor"], G_SCRIPT["TandavaSeveringTurnMonitor"], "turn monitor"),
                        (TG["settings"], G["settings"], "settings"),
                        (TG["rule"], G["rule"], "scoring rule"),
                        (TG["cell"], G["cell"], "cell config")):
    scene = swap(scene, old, new, label, expected=max(1, count_of(scene, old)))
g.emit_scene(SCENE_NAME, G["scene"], scene)

# ═════════════════════════════════════════════════════════════════════════════
# 6. The shared registries: a voyage is on the master roster and on no grid
# ═════════════════════════════════════════════════════════════════════════════
g.register_game_list(G["card"], lib.GAME_LIST)
g.register_always_unlocked()
g.register_build_scene("MinigameTandava", SCENE_NAME, G["scene"])


# ══ VALIDATE ═════════════════════════════════════════════════════════════════
def check_scene(sc):
    out = []
    for name, gd in (("controller", G_SCRIPT["TandavaSeveringController"]), ("turn monitor", G_SCRIPT["TandavaSeveringTurnMonitor"]),
                     ("settings", G["settings"]), ("scoring rule", G["rule"]), ("cell config", G["cell"])):
        if gd not in sc:
            out.append(f"the scene does not reference the fork's {name}")
    for k, gd in TS.items():
        if gd in sc:
            out.append(f"the scene still references Tandava's {k} script")
    for k in ("settings", "rule", "cell"):
        if TG[k] in sc:
            out.append(f"the scene still references Tandava's {k}")
    return out


errors += check_scene(scene)
for k, text in ((k, g.files[p]) for k, p in P.items()):
    for tk in ("swarm", "prefab", "species", "profile", "cell", "settings", "rule"):
        if TG[tk] in text:
            errors.append(f"{NAME[k]} still references Tandava's {tk}")
if G["species"] not in g.files[P["settings"]]:
    errors.append("the settings' SwarmSpawn is not the fork's species (the Severed would hatch as Tandava's swarm)")
if not re.search(rf"\bTandavaSevering = {MODE_ID},", g.read(GAME_MODES_CS)):
    errors.append("GameModes.TandavaSevering is missing")
for name, sid in zip(("TandavaSevered", "TandavaRejoined", "TandavaSuccession", "TandavaLearned", "TandavaChimera"), FORK_TOASTS):
    if sid in range(137, 148):
        errors.append(f"GameToastSituation.{name} ({sid}) overlaps Tandava's own toasts")
# the settings' variant / form structs carry the fields this script writes, in order
SO = FORK_SRC["TandavaSeveringSettingsSO"]
for struct, keys in (("TandavaSeveringFormSpec", ("DisplayName", "Role", "FillToEvolve", "BankShare", "MealVolume", "Line", "Variants")),
                     ("TandavaSeveringVariantSpec", ("DisplayName", "PlanIndex", "FeedPlanIndex", "CoilPlanIndices", "CoilMouths",
                                                     "CoilRoamRadius", "LungePlanIndex", "SnapPlanIndex", "LungeMouth", "Mouth",
                                                     "FeedMouth", "HaloCentre", "ChimeraPlanIndex", "ChimeraAltPlanIndex"))):
    body = SO.split(f"public struct {struct}")[1].split("\n    }")[0]
    if re.findall(r"public\s+[\w<>\[\]]+\s+(\w+)\s*;", body) != list(keys):
        errors.append(f"{struct}'s fields are not {keys} in order (the asset this script writes)")

referenced = dict({f"Tandava {k}": v for k, v in TG.items()}, **{f"Tandava script {k}": v for k, v in TS.items()})
minted = list(G.values()) + list(G_SCRIPT.values()) + list(G_PLAN.values())
g.finish(errors, referenced, minted)
