#!/usr/bin/env python3
"""
Author every serialized asset Tandava (GameModes.Tandava = 62) adds - the swarm arena race.
Assets/_Scripts/Controller/Arcade/TANDAVA.md is the design and status doc.

A tadpole swarm hatches at one end of a long cell and races for the exit membrane at the other, eating at oases on
the way. Every time its body is full and its stomach holds the surplus it takes its next FORM (Young Serpent ->
Serpent -> Great Serpent -> Bull), and the cell's membrane, nucleus and cytoplasm change colour at that moment. Every
pilot flies on ONE domain against it, in a Squirrel, a Sparrow or a Rhino.

What this script owns (one owner per file; the folders below are this script's alone):

  * the four form plans, baked by Tools/Build/tandava_plans.py (it validates them; this script refuses to write a
    plan that fails) - Assets/_SO_Assets/Swarm Fauna/Tandava/SwarmPlan_tandava_<kind>.json;
  * the swarm: TandavaSwarmFaunaConfig.asset (the Swarm cell's sort config with Tandava's numbers and the scripted
    plan list) and TandavaSwarmFauna.prefab (the sort anchor, pointing at it);
  * the Tandava cell (Assets/_SO_Assets/Cell Configs/Tandava Cell/): the cell config on the Cleave membrane, its
    spawn profile, the one swarm species and three Borromean flora FORKS planted into pens on the oases. A fork is
    owned here, not by author_borromean_flora_assets.py (whose DEPLOYMENTS do not list this cell - the Swarm and
    Wrecking Ball precedent);
  * the mode: TandavaSettings.asset (route, forms, palettes, narration), TandavaScoringRule.asset, the arena card,
    the toasts, the preview, MinigameTandava.unity (a clone of MinigameBroodRush), and the shared registries;
  * the .meta of every new script except ISwarmDirector.cs, which lives in the Swarm folder and so belongs to
    author_swarm_fauna.py (SCRIPTS there).

The route and the food numbers are the harness's (Tools/Build/swarm_core_harness/TandavaHarness.cs, T6-T9); the
validation below reads the harness's constants back and fails on any disagreement, so the model and the shipped
assets cannot drift apart silently.

Run:  python3 Tools/Build/author_tandava_assets.py [--check] [--self-test]
"""
import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arcade_mode_lib as lib          # noqa: E402
import tandava_plans                   # noqa: E402

MODE_ID = 62
g = lib.Generator(MODE_ID, "Tandava")
guid = lib.guid
num = lib.num
ROOT = lib.ROOT

# ── New scripts ──────────────────────────────────────────────────────────────
SCRIPT_PATHS = {
    "TandavaController":        "Assets/_Scripts/Controller/Arcade/Tandava/TandavaController.cs",
    "TandavaSettingsSO":        "Assets/_Scripts/Controller/Arcade/Tandava/TandavaSettingsSO.cs",
    "TandavaDirectorCore":      "Assets/_Scripts/Controller/Arcade/Tandava/TandavaDirectorCore.cs",
    "TandavaObjectiveProvider": "Assets/_Scripts/Controller/Arcade/Tandava/TandavaObjectiveProvider.cs",
    "TandavaScoringRuleSO":     "Assets/_Scripts/Controller/Arcade/Scoring/TandavaScoringRuleSO.cs",
    "TandavaTurnMonitor":       "Assets/_Scripts/Controller/Arcade/TurnMonitors/TandavaTurnMonitor.cs",
    "CellVisualTint":           "Assets/_Scripts/Controller/Environment/CellVisualTint.cs",
}
G_SCRIPT = {k: guid(f"script/{k}") for k in SCRIPT_PATHS}

# ── Folders this script owns ─────────────────────────────────────────────────
SWARM_DIR = "Assets/_SO_Assets/Swarm Fauna/Tandava"
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Tandava Cell"
MODE_DIR = "Assets/_Scripts/Controller/Arcade/Tandava"
PREFAB = "Assets/_Prefabs/FloraAndFauna/TandavaSwarmFauna.prefab"

# ── New assets ───────────────────────────────────────────────────────────────
FORM_KINDS = ["serpent_s", "serpent_m", "serpent_l", "bull"]
G_PLAN = {k: guid(f"asset/TandavaPlan_{k}") for k in FORM_KINDS}
G_ASSET = {
    "TandavaSwarmFaunaConfig":  guid("asset/TandavaSwarmFaunaConfig"),
    "TandavaSwarmFauna.prefab": guid("asset/TandavaSwarmFauna.prefab"),
    "SwarmSpecies":             guid("asset/TandavaSwarmSpecies"),
    "FloraEarlyMass":           guid("asset/TandavaFloraEarlyMass"),
    "FloraLateMass":            guid("asset/TandavaFloraLateMass"),
    "FloraLateSpace":           guid("asset/TandavaFloraLateSpace"),
    "SpawnProfile":             guid("asset/TandavaCellSpawnProfile"),
    "CellConfig":               guid("asset/TandavaCellConfig"),
    "TandavaSettings":          guid("asset/TandavaSettings"),
    "TandavaScoringRule":       guid("asset/TandavaScoringRule"),
    "ArcadeGameTandava":        guid("asset/ArcadeGameTandava"),
    "GameToastConfigTandava":   guid("asset/GameToastConfigTandava"),
    "ModePreviewTandava":       guid("asset/ModePreviewTandava"),
    "MinigameTandava.unity":    guid("asset/MinigameTandava.unity"),
}

# ── Existing GUIDs (read from the repo, never invented) ─────────────────────
EXISTING = dict(lib.SCRIPT_GUIDS)
EXISTING.update(lib.CARD_ART)
# The card's backdrop is RENDERED from this cell (render_card_backgrounds.py, the /cardart step) and wired by
# author_card_backgrounds.py; this script keeps whatever that render is, and the shared placeholder until it exists.
CARD_BACKGROUND = "Assets/_Graphics/ARCADE/CardBackgrounds/Tandava.png"
if os.path.exists(os.path.join(lib.ROOT, CARD_BACKGROUND + ".meta")):
    EXISTING["CardBackground"] = lib.existing_guid(CARD_BACKGROUND)
for hull in ("Rhino", "Squirrel", "Sparrow"):
    EXISTING[f"Vessel_{hull}"] = lib.VESSELS[hull]
SWARM_SCRIPTS = "Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm"
EXISTING["SwarmFaunaConfigSO"] = lib.existing_guid(f"{SWARM_SCRIPTS}/SwarmFaunaConfigSO.cs")
EXISTING["SwarmFauna"] = lib.existing_guid(f"{SWARM_SCRIPTS}/SwarmFauna.cs")
EXISTING["FaunaConfigurationSO"] = lib.existing_guid("Assets/_Scripts/Utility/DataContainers/FaunaConfigurationSO.cs")
SORT_CONFIG = "Assets/_SO_Assets/Swarm Fauna/SwarmSortFaunaConfig.asset"
SORT_PREFAB = "Assets/_Prefabs/FloraAndFauna/SwarmSortFauna.prefab"
EXISTING["SwarmSortFaunaConfig"] = lib.existing_guid(SORT_CONFIG)
EXISTING["SwarmSortFauna.prefab"] = lib.existing_guid(SORT_PREFAB)
EXISTING["CleaveMembrane"] = lib.existing_guid("Assets/_Prefabs/Environment/CleaveMembrane.prefab")
EXISTING["Nucleus"] = lib.CELL_VISUALS["NucleusPrefab"]
EXISTING["Cytoplasm"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
EXISTING["ExtraOmniCrystals"] = "daa37ae0e7af4b04383c1c4e6e76817d"   # the Swarm cell's modifier
CANON_FLORA = {e: f"Assets/_SO_Assets/Lifeforms/Borromean Flora {e}.asset" for e in ("Mass", "Space")}
for e, p in CANON_FLORA.items():
    EXISTING[f"Borromean{e}"] = lib.existing_guid(p)

# The donor scene (Brood Rush): the other swarm-of-fauna arena, same HasEndGame shape (results through a ClientRpc),
# same single cell, same AI-backfill initializer.
DONOR_SCENE = f"{lib.SCENES_DIR}/MinigameBroodRush.unity"
DONOR = {
    "controller": lib.existing_guid("Assets/_Scripts/Controller/Arcade/BroodRushController.cs"),
    "monitor":    lib.existing_guid("Assets/_Scripts/Controller/Arcade/TurnMonitors/BroodRushWaveTurnMonitor.cs"),
    "rule":       "25b19679987c42989adea3f3cb3cceca",   # BroodRushScoringRule.asset
    "wave":       "e54a4036a4024f58839d0d88a2e7e57e",   # its fauna-wave SOAP event
    "cell":       "6d6a9a4d9989498780367424e85848a4",   # the Brood Rush cell config
}
for k, v in DONOR.items():
    EXISTING[f"donor_{k}"] = v

# ═════════════════════════════════════════════════════════════════════════════
# THE NUMBERS
# ═════════════════════════════════════════════════════════════════════════════
MEMBRANE_RADIUS = 3600.0         # CleaveMembrane (a x3 similarity of the standard 1,200 membrane)
NUCLEUS_RADIUS = 392.0           # the standard nucleus prefab, unscaled (Cell.nucleusScaleMultiplier 1 in the scene)
CYTOPLASM_SHARD_DISTANCE = 360   # ~4,200 motes in a 3,600 u cell (the prefab's 120 would build ~113,000)

# The swarm (TandavaSwarmFaunaConfig) - the harness's numbers (TandavaHarness.cs; asserted below)
DENSITY = 3                      # the design's budget note: PlanDensity 3
SEED_MEMBERS = 48                # x DENSITY at hatch
BITERS_PER_STEP = 1              # the 2026-10-06 sweep: one biter every tick is the intake that holds T6-T9
CRUISE = 2.5                     # voxels/step: 2.5 x UnitScale 2 x 10 Hz = 50 u/s
UNIT_SCALE, PRISM_SCALE = 2.0, 1.0
EGG = [20.45, 40.31, 22.18, 12.8]  # Charge, Mass, Space, Time (the Swarm cell's measured egg volumes)
STOMACH_EGGS = 240
SURPLUS_FACTOR = 0.4             # the design: "plan count reached plus 40% of that again in banked volume"
MEALS_PER_FORM = 2.5             # the design: "the swarm needs 2 to 3 [oases] per stage"
FILL_TO_EVOLVE = 0.9
BODY_FILL = 0.939                # SortBodyFill (the sort core's grown body)
MAX_PROXIES = 160
# MultiDomain OFF, against the design's "MultiDomain on": a lineage swarm gives its slots 1 and 2 the OTHER two
# playable domains (SwarmFauna.BuildSlotDomains), and with every pilot on one domain one of those IS the pilots'.
# A drifted lineage would grow members in the pilots' own colour - an enemy that reads as a friend. One colour (the
# cell's hostile controller) until a lineage can exclude the pilots' domain.
MULTI_DOMAIN = 0
# MacroLod OFF: a collapsed swarm stops its worker tick, and the director runs on published ticks
# (ISwarmDirector.OnTickPublished) - a race the pilots fell behind in would freeze.
MACRO_LOD = 0

# The route (world, the cell's frame) - TandavaRoute in the harness
START_X, EXIT_X = -2000.0, 2000.0
OASIS_X = [-1650, -1250, -900, -550, 550, 900, 1250, 1650]
OASIS_RADIUS = 100.0
PLANTS_AT = [2, 2, 2, 2, 3, 3, 3, 3]       # Mass plants per oasis (early 1-4, late 5-8)
SPACE_AT = [0, 0, 0, 0, 1, 1, 1, 1]        # the design's "mixed later": one Space plant at each late oasis
PRISMS_PER_PLANT = 60                       # MaxTotalSpawnedObjectsOverride: a lobby can burn an oasis out
MAX_FEED_SECONDS = 45.0

# Pilots line up behind the hatch, facing down the course (Y +90 deg)
SPAWN_X = -2300.0
SPAWN_Z = [-90.0, -30.0, 30.0, 90.0]
SPAWN_ROT = (0.0, 0.7071068, 0.0, 0.7071068)

# The phase ladder (the Swarm cell's ratios against this cell's modelled mature mass)
RESTLESS_ENTER, RESTLESS_EXIT, FRENZY_ENTER, FRENZY_EXIT = 0.35, 0.26, 2.5, 2.2
COLLIDER_CEILING = 1200

BREAK_PERCENT = 35   # EndConditionOverridesSO.DefaultTandavaBreakPercent (asserted)

# ── Palettes (HDR: above 1 glows). The cell's own: membrane (0.37, 0.40, 0.96)/(0, 0.03, 1), cage edge
# (0, 0.02, 0.75, a 0.41), cage line (0.33, 0.35, 0.75, a 0.41), motes (0.38, 0.38, 0.5).
def pal(bright, dull, edge, straight, cyto):
    return dict(MembraneBright=bright, MembraneDull=dull, NucleusEdge=edge, NucleusStraight=straight, Cytoplasm=cyto)

FORMS = [
    dict(kind="serpent_s", name="Young Serpent",
         palette=pal((0.20, 0.85, 0.80, 1), (0.0, 0.45, 0.42, 1), (0.0, 0.55, 0.50, 0.41), (0.25, 0.70, 0.65, 0.41),
                     (0.30, 0.50, 0.48, 1)),
         line="The brood knots into a young serpent. It is hungry."),
    dict(kind="serpent_m", name="Serpent",
         palette=pal((0.15, 0.90, 0.45, 1), (0.0, 0.50, 0.20, 1), (0.0, 0.60, 0.25, 0.43), (0.20, 0.72, 0.35, 0.43),
                     (0.28, 0.50, 0.34, 1)),
         line="The serpent grows longer. Burn what it means to eat."),
    dict(kind="serpent_l", name="Great Serpent",
         palette=pal((0.35, 1.10, 0.60, 1), (0.0, 0.38, 0.18, 1), (0.05, 0.50, 0.25, 0.45), (0.30, 0.80, 0.45, 0.45),
                     (0.25, 0.45, 0.30, 1)),
         line="A great serpent now. One more meal and it will change."),
    dict(kind="bull", name="Bull",
         palette=pal((1.30, 0.45, 0.10, 1), (0.60, 0.12, 0.0, 1), (0.75, 0.20, 0.0, 0.45), (0.90, 0.45, 0.15, 0.45),
                     (0.55, 0.32, 0.22, 1)),
         line="The coils fold. A bull stands where the serpent was. Break it."),
]
ESCAPED_PALETTE = pal((1.20, 0.08, 0.15, 1), (0.55, 0.0, 0.05, 1), (0.70, 0.0, 0.05, 0.5), (0.85, 0.20, 0.25, 0.5),
                      (0.50, 0.18, 0.20, 1))

# ═════════════════════════════════════════════════════════════════════════════
# Derived
# ═════════════════════════════════════════════════════════════════════════════
PLANS = tandava_plans.bake_all()
plan_errors = tandava_plans.validate(PLANS)
STOMACH_CAPACITY = STOMACH_EGGS * DENSITY * sum(EGG) * 0.25   # SwarmFauna.StomachCapacityVolume


def mix(plan):
    m = [0, 0, 0, 0]
    for e in plan["elem"]:
        m[e] += 1
    return m


def build_forms():
    """The forms' banks and meals - TandavaHarness.BuildForms, the same arithmetic on the same upsampled bodies
    (SwarmPlanData.Upsample(m) is exactly m copies of every unit, so N and the mix scale by DENSITY).

    The bank is the design's surplus of the form's own body in banked Mass. A STAGE is the NEW food the form must eat
    to evolve: grow from the body it arrived with (the previous form's evolve fill, or the hatch) to its own evolve fill
    at its own mix's egg prices, plus its bank, less the previous form's bank (which the commit spends on exactly
    that growth). A meal is the stage over MEALS_PER_FORM, so a denied oasis is a meal the stage is short."""
    out, arrived, banked = [], SEED_MEMBERS * DENSITY, 0.0
    for k, f in enumerate(FORMS):
        p = PLANS[f["kind"]]
        n = p["n"] * DENSITY
        m = [x * DENSITY for x in mix(p)]
        avg_egg = sum(m[e] * EGG[e] for e in range(4)) / n
        fill_to = FILL_TO_EVOLVE * n
        bank = min(SURPLUS_FACTOR * n * EGG[1], 0.9 * STOMACH_CAPACITY) if k < len(FORMS) - 1 else 0.0
        stage = max(0.0, max(0.0, fill_to - arrived) * avg_egg + bank - banked)
        out.append(dict(f, n=n, bank=round(bank, 1), stage=stage, meal=round(max(1.0, stage / MEALS_PER_FORM), 1)))
        arrived, banked = fill_to, bank
    return out


FORM_ROWS = build_forms()


def leaf_volume(path):
    lx, ly, lz = map(float, re.search(r"LeafSize: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+)\}", g.read(path)).groups())
    return lx * ly * lz


MASS_LEAF = leaf_volume(CANON_FLORA["Mass"])
SPACE_LEAF = leaf_volume(CANON_FLORA["Space"])


def body_volume(plan):
    m3 = (2.0 * UNIT_SCALE * PRISM_SCALE) ** 3          # SwarmTickJob: scale = 2 x unit x prismScale x half
    h = plan["half"]
    return DENSITY * BODY_FILL * sum(m3 * abs(h[3 * k] * h[3 * k + 1] * h[3 * k + 2]) for k in range(plan["n"]))


PLANTS_MASS = sum(PLANTS_AT)
PLANTS_SPACE = sum(SPACE_AT)
FOREST_PRISMS = (PLANTS_MASS + PLANTS_SPACE) * PRISMS_PER_PLANT
FOREST_VOLUME = PRISMS_PER_PLANT * (PLANTS_MASS * MASS_LEAF + PLANTS_SPACE * SPACE_LEAF)
BIGGEST_BODY = max(body_volume(PLANS[k]) for k in FORM_KINDS)


def ladder():
    rt = lambda x, st: int(math.ceil(x / st) * st)
    prisms = FOREST_PRISMS + MAX_PROXIES
    volume = FOREST_VOLUME + BIGGEST_BODY
    return {
        "RestlessEnter": rt(prisms * RESTLESS_ENTER, 100), "RestlessExit": rt(prisms * RESTLESS_EXIT, 100),
        "FrenzyEnter": rt(prisms * FRENZY_ENTER, 100), "FrenzyExit": rt(prisms * FRENZY_EXIT, 100),
        "RestlessEnterVolume": rt(volume * RESTLESS_ENTER, 1000), "RestlessExitVolume": rt(volume * RESTLESS_EXIT, 1000),
        "FrenzyEnterVolume": rt(volume * FRENZY_ENTER, 1000), "FrenzyExitVolume": rt(volume * FRENZY_EXIT, 1000),
    }


def g5(x):
    s = f"{x:.5f}".rstrip("0").rstrip(".")
    return s if s and s != "-0" else "0"


def colour(c):
    return "{r: %s, g: %s, b: %s, a: %s}" % tuple(num(float(v)) for v in c)


def yaml_str(s):
    """A one-line YAML scalar the way Unity writes it: bare unless it needs quoting."""
    if re.search(r"[:#'\"{}\[\],&*!|>%@`]", s) or s != s.strip() or not s:
        return "'" + s.replace("'", "''") + "'"
    return s


# ═════════════════════════════════════════════════════════════════════════════
# 0. Metas
# ═════════════════════════════════════════════════════════════════════════════
for d in (MODE_DIR, SWARM_DIR, CELL_DIR):
    g.folder_meta(d)
g.text_meta("Assets/_Scripts/Controller/Arcade/TANDAVA.md")
for k, p in SCRIPT_PATHS.items():
    g.script_meta(p, G_SCRIPT[k])

TEXT_META = ("fileFormatVersion: 2\nguid: {}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n"
             "  assetBundleName: \n  assetBundleVariant: \n")

# ═════════════════════════════════════════════════════════════════════════════
# 1. The form plans
# ═════════════════════════════════════════════════════════════════════════════
for k in FORM_KINDS:
    rel = f"{SWARM_DIR}/SwarmPlan_tandava_{k}.json"
    g.emit(rel, tandava_plans.dumps(PLANS[k]))
    g.emit(rel + ".meta", TEXT_META.format(G_PLAN[k]))

# ═════════════════════════════════════════════════════════════════════════════
# 2. The swarm config - the Swarm cell's SORT config, with Tandava's numbers and the scripted list
# ═════════════════════════════════════════════════════════════════════════════
def set_key(text, key, value, label="swarm config"):
    text, n = re.subn(rf"(?m)^  {key}: .*$", f"  {key}: {value}", text)
    assert n == 1, f"{label}: '{key}' found {n} times"
    return text


def insert_after(text, key, block, label="swarm config"):
    m = re.search(rf"(?m)^  {key}: .*\n", text)
    assert m, f"{label}: '{key}' not found"
    return text[:m.end()] + block + text[m.end():]


cfg = g.read(SORT_CONFIG)
cfg = set_key(cfg, "m_Name", "TandavaSwarmFaunaConfig")
for key, value in (("Model", 2), ("MultiDomain", MULTI_DOMAIN), ("PlanDensity", DENSITY),
                   ("SeedMembers", SEED_MEMBERS), ("BitersPerStep", BITERS_PER_STEP), ("Cruise", num(CRUISE)),
                   ("UnitScale", num(UNIT_SCALE)), ("PrismScale", num(PRISM_SCALE)), ("StomachEggs", STOMACH_EGGS),
                   ("EggVolume", "{x: %s, y: %s, z: %s, w: %s}" % tuple(num(e) for e in EGG))):
    cfg = set_key(cfg, key, value)
# keys the shipped sort config predates (Unity has been reading their C# defaults): written explicitly here, in
# declaration order, because two of them are Tandava decisions
for key in ("ScriptedPlans", "ScriptedPlanPeriods", "MaxHitMaterialisationsPerFrame", "UnifiedPrismBodies", "MacroLod",
            "Bestiary", "HuntEnter", "LurkCalm", "LocustPhaseSeconds"):
    assert not re.search(rf"(?m)^  {key}:", cfg), f"the shipped sort config now carries '{key}' - set it, do not insert it"
cfg = insert_after(cfg, "TimePlan",
                   "  ScriptedPlans:\n" +
                   "".join(f"  - {{fileID: 4900000, guid: {G_PLAN[k]}, type: 3}}\n" for k in FORM_KINDS) +
                   "  ScriptedPlanPeriods:\n" +
                   "".join(f"  - {PLANS[k]['frameSteps']}\n" for k in FORM_KINDS))
cfg = insert_after(cfg, "ProxyLingerSeconds",
                   f"  MaxHitMaterialisationsPerFrame: 48\n  UnifiedPrismBodies: 1\n  MacroLod: {MACRO_LOD}\n")
cfg = insert_after(cfg, "DangerExit", "  Bestiary: 1\n  HuntEnter: 0.2\n  LurkCalm: 0.05\n  LocustPhaseSeconds: 2\n")
g.emit_asset(f"{SWARM_DIR}/TandavaSwarmFaunaConfig.asset", G_ASSET["TandavaSwarmFaunaConfig"], cfg)

# The anchor prefab: the sort anchor, pointing at the Tandava config
prefab = g.read(SORT_PREFAB)
prefab = lib.swap_guid(prefab, EXISTING["SwarmSortFaunaConfig"], G_ASSET["TandavaSwarmFaunaConfig"], "anchor config")
prefab, n = re.subn(r"(?m)^  m_Name: SwarmSortFauna$", "  m_Name: TandavaSwarmFauna", prefab)
assert n == 1, "anchor prefab: root name not found"
g.emit_prefab(PREFAB, G_ASSET["TandavaSwarmFauna.prefab"], prefab)
ANCHOR_MB_FID = re.search(r"--- !u!114 &(\d+)\n", prefab).group(1)

# ═════════════════════════════════════════════════════════════════════════════
# 3. The cell: species, flora forks, spawn profile, config
# ═════════════════════════════════════════════════════════════════════════════
def so(script, name):
    return lib.header_for(script, name)


g.emit_asset(f"{CELL_DIR}/Tandava Swarm Fauna Config Data.asset", G_ASSET["SwarmSpecies"],
             so(EXISTING["FaunaConfigurationSO"], "Tandava Swarm Fauna Config Data") +
             f"""  FaunaPrefab: {{fileID: {ANCHOR_MB_FID}, guid: {G_ASSET['TandavaSwarmFauna.prefab']}, type: 3}}
  InitialSpawnCount: 1
  PopulationSize: 1
  SpawnProbability: 1
  NetworkSynced: 0
  FeedsPerOffspring: 0
  OffspringPerBirth: 1
  ReproductionCooldownSeconds: 10
  MaxLivePopulation: 1
  ReleaseTier: 0
  BandInnerRadius: 0
  BandOuterRadius: 0
  CenterFocusBias: 0
  Element: 2
  Variant:
    Enabled: 0
  SpreadElements: 0
  ElementPalette: []
""")


def pens(oasis_ix):
    """One sector pen per oasis: a cone about the course axis through the oasis centre, between the oasis's near and
    far radius from the cell centre (FloraPlantingPen: fractions of the membrane radius)."""
    out = "  PlantingPens:\n"
    for o in oasis_ix:
        x = OASIS_X[o]
        d = abs(x)
        out += (f"  - Axis: {{x: {1 if x > 0 else -1}, y: 0, z: 0}}\n"
                f"    HalfAngle: {g5(math.degrees(math.atan(OASIS_RADIUS / d)))}\n"
                f"    InnerFraction: {g5((d - OASIS_RADIUS) / MEMBRANE_RADIUS)}\n"
                f"    OuterFraction: {g5((d + OASIS_RADIUS) / MEMBRANE_RADIUS)}\n")
    return out


def flora_fork(element, name, oasis_ix, per_oasis):
    """A FORK of the canonical Borromean species config: the species' own plate, quota, heart and shield byte for
    byte, with only what a CELL owns rewritten - the name, how many, where (one pen per oasis) and how big a plant
    may get here. Every plant it plants is a dispersed one, so the pens hold the floor."""
    count = per_oasis * len(oasis_ix)
    t = g.read(CANON_FLORA[element])
    t = re.sub(r"(?m)^  m_Name: .*$", f"  m_Name: {name}", t, count=1)
    for key, value in (("InitialSpawnCount", count), ("PopulationSize", count), ("MaxLivePopulation", count)):
        t, n = re.subn(rf"(?m)^  {key}: \d+$", f"  {key}: {value}", t, count=1)
        assert n == 1, f"{name}: {key} not found in the canonical config"
    t = re.sub(r"(?m)^  (PlantRadiusCellFraction(Max|Min)Override|MaxTotalSpawnedObjectsOverride|SpreadPlanting): .*\n", "", t)
    t = re.sub(r"(?m)^  PlantingPens:.*\n(?:  - .*\n|    .*\n)*", "", t)
    if not t.endswith("\n"):
        t += "\n"
    near = min(abs(OASIS_X[o]) for o in oasis_ix) - OASIS_RADIUS
    far = max(abs(OASIS_X[o]) for o in oasis_ix) + OASIS_RADIUS
    t += (f"  PlantRadiusCellFractionMaxOverride: {g5(far / MEMBRANE_RADIUS)}\n"
          f"  PlantRadiusCellFractionMinOverride: {g5(near / MEMBRANE_RADIUS)}\n"
          f"  MaxTotalSpawnedObjectsOverride: {PRISMS_PER_PLANT}\n"
          "  SpreadPlanting: 1\n" + pens(oasis_ix))
    return t


EARLY, LATE = [0, 1, 2, 3], [4, 5, 6, 7]
assert all(PLANTS_AT[o] == PLANTS_AT[EARLY[0]] for o in EARLY) and all(PLANTS_AT[o] == PLANTS_AT[LATE[0]] for o in LATE)
assert all(SPACE_AT[o] == 0 for o in EARLY) and all(SPACE_AT[o] == SPACE_AT[LATE[0]] for o in LATE)
FLORAS = [
    ("FloraEarlyMass", "Tandava Early Borromean Flora Mass Config Data", "Mass", EARLY, PLANTS_AT[EARLY[0]]),
    ("FloraLateMass", "Tandava Late Borromean Flora Mass Config Data", "Mass", LATE, PLANTS_AT[LATE[0]]),
    ("FloraLateSpace", "Tandava Late Borromean Flora Space Config Data", "Space", LATE, SPACE_AT[LATE[0]]),
]
for key, name, element, oases, per in FLORAS:
    g.emit_asset(f"{CELL_DIR}/{name}.asset", G_ASSET[key], flora_fork(element, name, oases, per))

g.emit_asset(f"{CELL_DIR}/Tandava Cell Spawn Profile.asset", G_ASSET["SpawnProfile"],
             so(lib.SCRIPT_GUIDS["SpawnProfileSO"], "Tandava Cell Spawn Profile") +
             "  FloraExcludeLocalDomain: 0\n  FloraSpawnVolumeCeiling: 12000\n  FloraInitialDelaySeconds: 0\n"
             "  FloraSpawnIntervalSeconds: 0\n  FloraPopulationScale: 1\n  FloraPlantBudgetScale: 1\n"
             "  FloraPrismScale: 1\n  SupportedFloras:\n" +
             "".join(f"  - {{fileID: 11400000, guid: {G_ASSET[k]}, type: 2}}\n" for k, *_ in FLORAS) +
             "  FaunaExcludeLocalDomain: 0\n  InitialFaunaSpawnWaitTime: 6\n  InitialFaunaReleaseTier: 2147483647\n"
             "  FaunaSpawnVolumeThreshold: 1\n  FaunaPopulationScale: 1\n  BaseFaunaSpawnTime: 30\n"
             "  SeedFullWaveEveryTick: 0\n  FaunaFoodFloor: 0\n  FaunaInitialDelaySeconds: 0\n"
             "  FaunaSpawnIntervalSeconds: 0\n  HerbivoreSpawnPointCount: 3\n  HerbivoreSpawnRadius: 800\n"
             "  PredatorSpawnPointCount: 0\n  PredatorSpawnRadius: 900\n  SupportedFaunas:\n"
             f"  - {{fileID: 11400000, guid: {G_ASSET['SwarmSpecies']}, type: 2}}\n")

LADDER = ladder()
g.emit_asset(f"{CELL_DIR}/Tandava Cell Config.asset", G_ASSET["CellConfig"],
             so(lib.SCRIPT_GUIDS["CellConfigDataSO"], "Tandava Cell Config") +
             f"""  CellName: Tandava
  Description: A long cell for one swarm's run - the Cleave membrane, eight Borromean oases along the
    course, and an exit at the far end. Authored by Tools/Build/author_tandava_assets.py
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: 2
  CellEndGameScore: 0
  MembranePrefab: {{fileID: 346633111830028674, guid: {EXISTING['CleaveMembrane']}, type: 3}}
  NucleusPrefab: {{fileID: 7555898194514117247, guid: {EXISTING['Nucleus']}, type: 3}}
  CytoplasmPrefab: {{fileID: 639495419069806261, guid: {EXISTING['Cytoplasm']}, type: 3}}
  CytoplasmShardDistance: {CYTOPLASM_SHARD_DISTANCE}
  CellModifiers:
  - {{fileID: 8058406376250941529, guid: {EXISTING['ExtraOmniCrystals']}, type: 3}}
  SpawnProfile: {{fileID: 11400000, guid: {G_ASSET['SpawnProfile']}, type: 2}}
  BootDefault: 0
  EnvironmentPrefab: {{fileID: 0}}
  EnvironmentIntensity: 1
  SenseRadiusOverride: 0
  PetalBurnRule: 0
  initialControllingDomain: 1
  conservedFaunaStomach: 0
  PhaseThresholds:
""" + "".join(f"    {k}: {v}\n" for k, v in LADDER.items()))

# ═════════════════════════════════════════════════════════════════════════════
# 4. The mode
# ═════════════════════════════════════════════════════════════════════════════
def v3(x, y=0.0, z=0.0):
    return f"{{x: {num(float(x))}, y: {num(float(y))}, z: {num(float(z))}}}"


def palette_yaml(p, indent):
    return "".join(f"{indent}{k}: {colour(p[k])}\n"
                   for k in ("MembraneBright", "MembraneDull", "NucleusEdge", "NucleusStraight", "Cytoplasm"))


forms_yaml = ""
for k, f in enumerate(FORM_ROWS):
    forms_yaml += (f"  - DisplayName: {yaml_str(f['name'])}\n    PlanIndex: {k}\n    FillToEvolve: {num(FILL_TO_EVOLVE)}\n"
                   f"    BankToEvolve: {{x: 0, y: {num(f['bank'])}, z: 0, w: 0}}\n    MealVolume: {num(f['meal'])}\n"
                   f"    Palette:\n{palette_yaml(f['palette'], '      ')}    Line: {yaml_str(f['line'])}\n")

g.emit_asset("Assets/_SO_Assets/Games/TandavaSettings.asset", G_ASSET["TandavaSettings"],
             so(G_SCRIPT["TandavaSettingsSO"], "TandavaSettings") +
             f"  SwarmConfig: {{fileID: 11400000, guid: {G_ASSET['TandavaSwarmFaunaConfig']}, type: 2}}\n"
             f"  StartPoint: {v3(START_X)}\n  StartHeading: {v3(1)}\n  Oases:\n" +
             "".join(f"  - Centre: {v3(x)}\n    Radius: {num(OASIS_RADIUS)}\n" for x in OASIS_X) +
             f"  ExitPoint: {v3(EXIT_X)}\n  ExitNormal: {v3(1)}\n"
             f"  ArriveMargin: 60\n  LeaveWhenStomachFill: 0.98\n  GiveUpSeconds: 8\n  MaxFeedSeconds: {num(MAX_FEED_SECONDS)}\n"
             "  BreakArmFraction: 0.6\n  DenialCheckSeconds: 1\n  Forms:\n" + forms_yaml +
             "  TransitionSeconds: 3\n  TransitionFlash: 1.5\n  EscapedPalette:\n" + palette_yaml(ESCAPED_PALETTE, "    ") +
             "  RestoreSeconds: 5\n"
             "  StartLine: Something in the reef remembers the old shapes.\n"
             "  HeadingForExitLine: Its last meal is behind it. Stop it before the membrane.\n"
             "  EscapedLine: The cycle ends. Too soon.\n"
             "  WonLine: The dance is broken. The reef keeps its turn.\n"
             "  NudgeThreshold: 25\n  NudgeFraction: 0.2\n  MaxNudge: 6\n")

g.emit_asset("Assets/_SO_Assets/Scoring Rules/TandavaScoringRule.asset", G_ASSET["TandavaScoringRule"],
             so(G_SCRIPT["TandavaScoringRuleSO"], "TandavaScoringRule") + "  metric: 7\n  golfRules: 0\n")

CARD_HULLS = ("Rhino", "Squirrel", "Sparrow")
g.emit_asset("Assets/_SO_Assets/Games/ArcadeGameTandava.asset", G_ASSET["ArcadeGameTandava"],
             so(EXISTING["SO_ArcadeGame"], "ArcadeGameTandava") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Tandava
  Description: A tadpole swarm races down a long cell for the exit, eating at every oasis. Each
    time it has eaten enough it takes a new form - a serpent that grows, then a horned bull - and
    the whole cell changes colour with it. Cull it, burn the oasis ahead, and stop it before the
    membrane. Everyone flies together.
  IconActive: {{fileID: 21300000, guid: {EXISTING['IconActive']}, type: 3}}
  IconInactive: {{fileID: 21300000, guid: {EXISTING['IconInactive']}, type: 3}}
  CardBackground: {{fileID: 21300000, guid: {EXISTING['CardBackground']}, type: 3}}
  GolfScoring: 0
  SceneName: MinigameTandava
  Vessels:
""" + "".join(f"  - {{fileID: 11400000, guid: {EXISTING[f'Vessel_{h}']}, type: 2}}\n" for h in CARD_HULLS) + """  MinPlayersAllowed: 1
  MaxPlayersAllowed: 6
  MinDomainsAllowed: 1
  MaxDomainsAllowed: 1
  MinIntensity: 1
  MaxIntensity: 4
  ArenaRules: 1
  Tips:
  - Burn the oasis ahead before it gets there. A swarm that cannot eat cannot change.
  - It changes form only when its body is full and its stomach holds the surplus. Culling sets both back.
  - The final form breaks when you cut it below a third of its body.
  - The cell changes colour the moment the swarm changes form. Watch the membrane.
  ViewUserAction: 0
  PlayUserAction: 0
  StartingElements: []
  ComebackRatePerScoreDeficit: 0
""")

g.emit_asset("Assets/_SO_Assets/Game Toasts/GameToastConfig_Tandava.asset", G_ASSET["GameToastConfigTandava"],
             so(EXISTING["GameToastConfigSO"], "GameToastConfig_Tandava") +
             f"  gameMode: {MODE_ID}\n  toasts:\n" +
             lib.toast(128, "{0}", domain_names=0) +
             lib.toast(129, "{0}", domain_names=0) +
             lib.toast(130, "{0}", domain_names=0) +
             lib.toast(131, "{0}", domain_names=0) +
             lib.toast(132, "{0}", domain_names=0) +
             lib.toast(133, "Burn the oasis ahead - a swarm that cannot eat cannot change", domain_names=0,
                       idle=1, idle_seconds=40))
g.register_toast_config(G_ASSET["GameToastConfigTandava"])


def fmt4(x):
    return f"{x:.4f}".rstrip("0").rstrip(".") or "0"


g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Tandava.asset", G_ASSET["ModePreviewTandava"],
             so(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Tandava") + f"""  Mode: {MODE_ID}
  Notes: 'The Tandava cell as the mode flies it - the oases, the swarm, the start line. No director runs
    in a preview, so the swarm wanders as its first form rather than racing, and the cell keeps its own
    colours. Vessel is -1 (ANY): the carousel''s pick flies it.'
  PreviewCell: {{fileID: 11400000, guid: {G_ASSET['CellConfig']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  Vessel: -1
  ObjectiveText: Stop the swarm before the membrane
  ObjectiveMetric: 7
  ObjectiveTarget: 0
  DurationSeconds: 60
  SpawnFromCellRing: 0
  SpawnDistanceOutsideNucleus: 40
  SpawnRingRadiusFloor: 0
  SpawnFormation: 0
  SpawnPoints:
""" + "".join(f"  - position: {{x: {fmt4(SPAWN_X)}, y: 0, z: {fmt4(z)}}}\n"
              f"    rotation: {{x: 0, y: {fmt4(SPAWN_ROT[1])}, z: 0, w: {fmt4(SPAWN_ROT[3])}}}\n" for z in SPAWN_Z))
g.register_preview(G_ASSET["ModePreviewTandava"])

# ═════════════════════════════════════════════════════════════════════════════
# 5. The scene: clone MinigameBroodRush - a ONE-SHOT that stands down once its donor moves on
# ═════════════════════════════════════════════════════════════════════════════
SCENE_REL = f"{lib.SCENES_DIR}/MinigameTandava.unity"
SPAWN_FIDS = ["1468661147", "1074736317", "1323644424", "1564881929"]


def move_transform(text, fid, pos, rot):
    m = re.search(rf"--- !u!4 &{fid}\n(?:(?!--- !u!).*\n)*", text)
    assert m, f"spawn transform {fid} not found in the donor"
    doc = m.group(0)
    new = re.sub(r"m_LocalRotation: \{[^}]*\}", "m_LocalRotation: {x: %s, y: %s, z: %s, w: %s}" % rot, doc)
    new = re.sub(r"m_LocalPosition: \{[^}]*\}", "m_LocalPosition: {x: %s, y: %s, z: %s}" % pos, new)
    new = re.sub(r"m_LocalEulerAnglesHint: \{[^}]*\}", "m_LocalEulerAnglesHint: {x: 0, y: 90, z: 0}", new)
    assert new != doc, f"spawn transform {fid} unchanged"
    return text.replace(doc, new, 1)


def clone_scene():
    scene = g.read(DONOR_SCENE)
    scene = lib.swap_guid(scene, DONOR["monitor"], G_SCRIPT["TandavaTurnMonitor"], "turn monitor")
    scene = lib.swap_guid(scene, DONOR["controller"], G_SCRIPT["TandavaController"], "controller")
    scene = lib.replace_block(
        scene,
        f"  rule: {{fileID: 11400000, guid: {DONOR['rule']}, type: 2}}\n"
        f"  onFaunaWaveSpawned: {{fileID: 11400000, guid: {DONOR['wave']}, type: 2}}\n",
        f"  settings: {{fileID: 11400000, guid: {G_ASSET['TandavaSettings']}, type: 2}}\n"
        f"  rule: {{fileID: 11400000, guid: {G_ASSET['TandavaScoringRule']}, type: 2}}\n",
        "controller field block")
    # THE AI HULL: Brood Rush's four Squirrel templates become RANDOM (0), so PickAIVesselType draws each bot's hull
    # from the card's three
    scene = lib.swap_guid(scene, "  - vesselClass: 6\n", "  - vesselClass: 0\n", "AI templates", expected=4)
    scene = lib.swap_guid(scene, DONOR["cell"], G_ASSET["CellConfig"], "cell config")
    # a start LINE behind the hatch, not a ring round the nucleus
    scene = lib.replace_block(scene, "  arrangeSpawnPointsAroundCell: 1\n", "  arrangeSpawnPointsAroundCell: 0\n",
                              "spawn ring flag")
    for fid, z in zip(SPAWN_FIDS, SPAWN_Z):
        scene = move_transform(scene, fid, (num(SPAWN_X), 0, num(z)), tuple(num(v) for v in SPAWN_ROT))
    return scene


try:
    SCENE = clone_scene()
    SCENE_SOURCE = "cloned from " + DONOR_SCENE
except AssertionError as e:
    # The donor moved on (a later edit to Brood Rush's scene). The clone is a one-shot: stand down, keep the
    # committed scene, and let every check below describe IT - never abort the validation with the migration.
    if not os.path.exists(os.path.join(ROOT, SCENE_REL)):
        raise
    SCENE = g.read(SCENE_REL)
    SCENE_SOURCE = f"committed scene kept (the donor no longer matches the one-shot clone: {e})"
g.emit_scene("MinigameTandava", G_ASSET["MinigameTandava.unity"], SCENE)

# ═════════════════════════════════════════════════════════════════════════════
# 6. The shared registries
# ═════════════════════════════════════════════════════════════════════════════
g.register_arena_card(G_ASSET["ArcadeGameTandava"])        # master + ARENA grid, never Arcade
g.register_always_unlocked()
g.register_build_scene("MinigameBroodRush", "MinigameTandava", G_ASSET["MinigameTandava.unity"])
g.set_end_condition("tandavaBreakPercent", after="broadsidePointsPerPilot", value=BREAK_PERCENT)


# ══ VALIDATE EVERYTHING BEFORE WRITING ANYTHING ═════════════════════════════
def validate_scene(sc):
    """Every promise the scene makes, as failure strings. Run on the shipped scene AND (--self-test) on the donor,
    where every one of them must fire."""
    errs = []
    for k in ("controller", "monitor", "rule", "wave", "cell"):
        if DONOR[k] in sc:
            errs.append(f"scene still references the Brood Rush {k}")
    for label, gd in (("TandavaController", G_SCRIPT["TandavaController"]), ("TandavaTurnMonitor", G_SCRIPT["TandavaTurnMonitor"]),
                      ("TandavaSettings", G_ASSET["TandavaSettings"]), ("TandavaScoringRule", G_ASSET["TandavaScoringRule"]),
                      ("the Tandava cell", G_ASSET["CellConfig"])):
        if sc.count(gd) != 1:
            errs.append(f"scene references {label} {sc.count(gd)} times (expected 1)")
    if sc.count("  - vesselClass: 0\n") != 4 or re.search(r"  - vesselClass: [1-9]\d*\n", sc):
        errs.append("scene does not carry exactly 4 RANDOM-hull AI templates")
    if "  arrangeSpawnPointsAroundCell: 0\n" not in sc:
        errs.append("scene still spawns pilots round the nucleus (arrangeSpawnPointsAroundCell)")
    for fid, z in zip(SPAWN_FIDS, SPAWN_Z):
        m = re.search(rf"--- !u!4 &{fid}\n(?:(?!--- !u!).*\n)*", sc)
        if not m or f"m_LocalPosition: {{x: {num(SPAWN_X)}, y: 0, z: {num(z)}}}" not in m.group(0):
            errs.append(f"spawn point {fid} is not on the start line at x = {num(SPAWN_X)}, z = {num(z)}")
    return errs


errors = [f"plan: {e}" for e in plan_errors]
errors += validate_scene(g.files[SCENE_REL])

# the forms: an order of growing bodies, each a stage the route can feed
if [f["kind"] for f in FORM_ROWS] != FORM_KINDS:
    errors.append("the forms are not the plans in order")
for a, b in zip(FORM_ROWS, FORM_ROWS[1:]):
    if a["n"] >= b["n"] and b["kind"] != "bull":
        errors.append(f"{b['name']} ({b['n']}) is not bigger than {a['name']} ({a['n']})")
for f in FORM_ROWS[:-1]:
    if not 0 < f["bank"] < STOMACH_CAPACITY:
        errors.append(f"{f['name']}: bank {f['bank']} outside (0, stomach capacity {STOMACH_CAPACITY:.0f})")
    if f["meal"] <= 1:
        errors.append(f"{f['name']}: meal {f['meal']} - a stop that eats nothing")
if FORM_ROWS[-1]["bank"] != 0:
    errors.append("the final form must bank nothing (there is no next form)")
if max(f["n"] for f in FORM_ROWS) > 1000:
    errors.append("a form over 1,000 tadpoles (round 7's per-swarm ceiling)")

# the food: the oases at their floor must pay every stage's Mass (a grazed plant regrows; this is the floor)
mass_bill = sum(f["stage"] for f in FORM_ROWS)
mass_food = PLANTS_MASS * PRISMS_PER_PLANT * MASS_LEAF
if mass_food < mass_bill:
    errors.append(f"the route's Mass ({mass_food:,.0f}) cannot pay the stages ({mass_bill:,.0f})")

# the route: inside the membrane, outside the nucleus, in order
if not (-MEMBRANE_RADIUS < START_X < OASIS_X[0] and OASIS_X == sorted(OASIS_X) and OASIS_X[-1] < EXIT_X < MEMBRANE_RADIUS):
    errors.append("the route is not start < oases (ascending) < exit inside the membrane")
for x in OASIS_X:
    if abs(x) - OASIS_RADIUS <= NUCLEUS_RADIUS:
        errors.append(f"the oasis at x = {x} reaches into the nucleus (fauna eat nothing there)")
if not -MEMBRANE_RADIUS < SPAWN_X < START_X:
    errors.append("the start line is not behind the hatch inside the membrane")
if len(SPAWN_Z) < len(CARD_HULLS):
    errors.append(f"{len(SPAWN_Z)} spawn points for {len(CARD_HULLS)} seats - GetRandomSpawnPose would stack pilots")

# the collider budget: always-on plant hearts + one swarm fully engaged (heart + body per proxy)
colliders = PLANTS_MASS + PLANTS_SPACE + 2 * MAX_PROXIES
if colliders >= COLLIDER_CEILING:
    errors.append(f"{colliders} colliders >= the ceiling {COLLIDER_CEILING}")

# the C# this script authors keys for
SO_SRC = {
    "SwarmFaunaConfigSO": g.read(f"{SWARM_SCRIPTS}/SwarmFaunaConfigSO.cs"),
    "CellConfigDataSO": g.read("Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs"),
    "FloraConfigurationSO": g.read("Assets/_Scripts/Utility/DataContainers/FloraConfigurationSO.cs"),
    "TandavaSettingsSO": g.read(SCRIPT_PATHS["TandavaSettingsSO"]),
}
for so_name, keys in (("SwarmFaunaConfigSO", ("ScriptedPlans", "ScriptedPlanPeriods", "MacroLod", "MultiDomain", "PlanDensity")),
                      ("CellConfigDataSO", ("CytoplasmShardDistance",)),
                      ("FloraConfigurationSO", ("MaxTotalSpawnedObjectsOverride", "PlantingPens", "SpreadPlanting")),
                      ("TandavaSettingsSO", ("SwarmConfig", "Oases", "MaxFeedSeconds", "Forms", "EscapedPalette", "MaxNudge"))):
    for key in keys:
        if not re.search(rf"\b{key}\b\s*(=|;)", SO_SRC[so_name]):
            errors.append(f"{so_name} no longer declares '{key}' - the key this script authors")
settings_keys = set(re.findall(r"(?m)^  (\w+):", g.files["Assets/_SO_Assets/Games/TandavaSettings.asset"])) - {
    "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Enabled",
    "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier"}
declared = set(re.findall(r"public\s+[\w<>\[\]]+\s+(\w+)\s*(?:=|;)", SO_SRC["TandavaSettingsSO"].split("class TandavaSettingsSO")[1]))
if settings_keys != declared:
    errors.append(f"TandavaSettings.asset keys differ from TandavaSettingsSO: extra {sorted(settings_keys - declared)}, "
                  f"missing {sorted(declared - settings_keys)}")

# the harness models the route and the swarm this script lays (TandavaHarness.cs) - read back, not trusted
H = g.read("Tools/Build/swarm_core_harness/TandavaHarness.cs")


def h_const(pattern, label):
    m = re.search(pattern, H)
    if not m:
        errors.append(f"harness: {label} not found")
        return None
    return m.group(1)


for label, pattern, ours in (
        ("Density", r"const int Density = (\d+);", DENSITY),
        ("SeedMembers", r"const int SeedMembers = (\d+);", SEED_MEMBERS),
        ("BitersPerStep", r"const int BitersPerStep = (\d+);", BITERS_PER_STEP),
        ("PrismsPerPlant", r"const int PrismsPerPlant = (\d+);", PRISMS_PER_PLANT),
        ("StartX", r"StartX = (-?[\d.]+)f", START_X), ("ExitX", r"ExitX = (-?[\d.]+)f", EXIT_X),
        ("Cruise", r'Env\("TANDAVA_CRUISE", ([\d.]+)f\)', CRUISE),
        ("MaxFeedSeconds", r'Env\("TANDAVA_MAX_FEED", ([\d.]+)f\)', MAX_FEED_SECONDS),
        ("SurplusFactor", r"SurplusFactor = ([\d.]+)f", SURPLUS_FACTOR),
        ("MealsPerForm", r"MealsPerForm = ([\d.]+)f", MEALS_PER_FORM)):
    v = h_const(pattern, label)
    if v is not None and abs(float(v) - float(ours)) > 1e-6:
        errors.append(f"harness {label} = {v}, this script authors {ours}")
for label, ours in (("OasisX", OASIS_X), ("PlantsAt", PLANTS_AT)):
    v = h_const(rf"{label} = \{{([^}}]*)\}}", label)
    if v is not None and [float(x) for x in v.split(",")] != [float(x) for x in ours]:
        errors.append(f"harness {label} = {{{v.strip()}}}, this script authors {ours}")
v = h_const(r"static readonly float\[\] EggVolume = \{([^}]*)\}", "EggVolume")
if v is not None and [float(x.strip().rstrip("f")) for x in v.split(",")] != EGG:
    errors.append(f"harness EggVolume {{{v.strip()}}} != {EGG}")

# the end condition, the enum ids, the toast situations
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultTandavaBreakPercent = (\d+);", eco)
if not m or int(m.group(1)) != BREAK_PERCENT:
    errors.append("EndConditionOverridesSO.DefaultTandavaBreakPercent differs from the authored break percent")
if not re.search(rf"\bTandava = {MODE_ID},", g.read("Assets/_Scripts/Data/Enums/GameModes.cs")):
    errors.append(f"GameModes.Tandava is not {MODE_ID}")
toasts_src = g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs")
for name, sid in (("TandavaMatchStart", 128), ("TandavaFormTaken", 129), ("TandavaEscaped", 130), ("TandavaBroken", 131),
                  ("TandavaHeadingForExit", 132), ("TandavaDenyHint", 133)):
    if not re.search(rf"\b{name} = {sid},", toasts_src):
        errors.append(f"GameToastSituation.{name} is not {sid}")

# the card is an ARENA card, single domain, its hulls only
card = g.files["Assets/_SO_Assets/Games/ArcadeGameTandava.asset"]
for h in CARD_HULLS:
    if EXISTING[f"Vessel_{h}"] not in card:
        errors.append(f"card missing {h}")
if G_ASSET["ArcadeGameTandava"] not in g.files.get(lib.ARENA_GRID, ""):
    errors.append("card is not on the ARENA grid")
if G_ASSET["ArcadeGameTandava"] in g.read_current(lib.ARCADE_GRID):
    errors.append("card is on the ARCADE grid (arena cards never are)")

# the swarm config really is scripted, sort, and dense enough
sw = g.files[f"{SWARM_DIR}/TandavaSwarmFaunaConfig.asset"]
for key, want in (("Model", "2"), ("PlanDensity", str(DENSITY)), ("MacroLod", str(MACRO_LOD)), ("MultiDomain", str(MULTI_DOMAIN)),
                  ("BitersPerStep", str(BITERS_PER_STEP)), ("Cruise", num(CRUISE))):
    if not re.search(rf"(?m)^  {key}: {re.escape(want)}$", sw):
        errors.append(f"swarm config {key} is not {want}")
if [x for x in re.findall(r"guid: ([0-9a-f]{32}), type: 3\}", sw.split("ScriptedPlans:")[1].split("ScriptedPlanPeriods:")[0])] \
        != [G_PLAN[k] for k in FORM_KINDS]:
    errors.append("swarm config's ScriptedPlans are not the four forms in order")

if "--self-test" in sys.argv:
    # NEGATIVE CONTROL: the scene checks must fire on the donor, every one of them
    donor_errs = validate_scene(g.read(DONOR_SCENE))
    expected = 5 + 5 + 1 + 1 + len(SPAWN_FIDS)
    print(f"self-test: the donor scene trips {len(donor_errs)} of {expected} scene checks")
    for e in donor_errs:
        print("   ", e)
    sys.exit(0 if len(donor_errs) == expected else 1)

# ── report ──
print("Tandava - the swarm's forms (density %d, stomach capacity %.0f):" % (DENSITY, STOMACH_CAPACITY))
for f in FORM_ROWS:
    print(f"  {f['name']:<14} {f['n']:>4} tadpoles  bank {f['bank']:>7,.1f}  stage {f['stage']:>8,.1f}  meal {f['meal']:>7,.1f}")
print(f"  route Mass {mass_food:,.0f} at the floor vs the stages' {mass_bill:,.0f};  forest {FOREST_PRISMS} prisms, "
      f"{FOREST_VOLUME:,.0f} volume;  biggest body {BIGGEST_BODY:,.0f} volume;  {colliders} colliders worst case")
print("  ladder: " + ", ".join(f"{k} {v:,}" for k, v in LADDER.items()))
print(f"  scene: {SCENE_SOURCE}")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()) + list(G_PLAN.values())
                + [guid("folder/" + d) for d in (MODE_DIR, SWARM_DIR, CELL_DIR)]
                + [guid("text/Assets/_Scripts/Controller/Arcade/TANDAVA.md")])
