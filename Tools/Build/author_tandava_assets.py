#!/usr/bin/env python3
"""
Author every serialized asset Tandava (GameModes.Tandava = 62) adds - the swarm hunt in a closed cell.
Assets/_Scripts/Controller/Arcade/TANDAVA.md is the design and status doc.

One creature, a tadpole swarm, hatches WHOLE as the Great Serpent inside a closed cell (the standard membrane is its
wall) with the cell's flora dispersed through the cytoplasm. It goes where it likes to eat, its speed and manner set by
how threatened it feels; feeding it puts its plates out round its mouth as danger guards and stops regrowing. Banked, it
takes its next form - the Many-Headed Serpent, then (rising where it stands) the Lord of the Dance inside a halo of
rings, then the Antlion, whose last feast completes the cycle. Every match draws one of three variants of each form.
Every pilot flies on ONE domain against it, in a Squirrel, a Sparrow or a Rhino.

What this script owns (one owner per file; the folders below are this script's alone):

  * the 51 form plans (every variant's travel plan, its strike pose, each serpent's three meal coils, and two lunge
    poses each for the Many-Headed Serpent - heads reared, heads thrown - and the Antlion - jaws wide, jaws shut), baked by
    Tools/Build/tandava_plans.py (it validates them; this script refuses to write a plan
    that fails) - Assets/_SO_Assets/Swarm Fauna/Tandava/SwarmPlan_tandava_<key>.json;
  * the swarm: TandavaSwarmFaunaConfig.asset (the Swarm cell's sort config with Tandava's numbers and the scripted plan
    list) and TandavaSwarmFauna.prefab (the sort anchor, pointing at it);
  * the Tandava cell (Assets/_SO_Assets/Cell Configs/Tandava Cell/): the cell config on the standard membrane, its spawn
    profile, the one swarm species and two Borromean flora FORKS dispersed through the cell. A fork is owned here, not by
    author_borromean_flora_assets.py (whose DEPLOYMENTS do not list this cell - the Swarm and Wrecking Ball precedent);
  * the mode: TandavaSettings.asset (the forms and their variants, the director's dials, the halo, the gold burst, the
    dance's palette, narration), TandavaScoringRule.asset, the arena card, the toasts, the preview, MinigameTandava.unity
    (a clone of MinigameBroodRush), and the shared registries;
  * the .meta of every new script except ISwarmDirector.cs, which lives in the Swarm folder and so belongs to
    author_swarm_fauna.py (SCRIPTS there).

The numbers are the harness's (Tools/Build/swarm_core_harness/TandavaHarness.cs, T1-T15): the validation below reads
the harness's constants back and fails on any disagreement, and the director's block in the settings asset IS the C#
defaults of TandavaDirectorSettings (read back too) - so the model and the shipped assets cannot drift apart silently.

Run:  python3 Tools/Build/author_tandava_assets.py [--check] [--self-test]
"""
import math
import os
import hashlib
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
    "TandavaHaloRing":          "Assets/_Scripts/Controller/Arcade/Tandava/TandavaHaloRing.cs",
    "TandavaGoldBurst":         "Assets/_Scripts/Controller/Arcade/Tandava/TandavaGoldBurst.cs",
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

# ── The plans: every variant of every form, and the feed twins ───────────────
PLAN_KEYS = tandava_plans.plan_keys()
G_PLAN = {k: guid(f"asset/TandavaPlan_{k}") for k in PLAN_KEYS}
# the first redesign's six plans and three oasis flora forks, retired by this one (the second redesign, 2026-10-06)
RETIRED = ([f"{SWARM_DIR}/SwarmPlan_tandava_{k}.json" for k in ("serpent_s", "serpent_m", "serpent_l", "bull", "dancer", "lion")]
           + [f"{CELL_DIR}/Tandava {n} Borromean Flora {e} Config Data.asset"
              for n, e in (("Early", "Mass"), ("Late", "Mass"), ("Late", "Space"))]
           + ["Assets/_Scripts/Controller/Arcade/Tandava/TandavaFlame.cs"]
           # the open 1,200 u cell's Space Borromean fork, retired by the crowded reef (the third pass, 2026-10-06)
           + [f"{CELL_DIR}/Tandava Borromean Flora Space Config Data.asset"]
           # the third pass's environment (Crystal Capture's Atlantis, thinned), removed at the prompter's word (2026-10-06)
           + ["Assets/_Prefabs/Spawnables/SpawnableAtlantis Tandava.prefab"]
           # the Sea Lion, replaced by the Antlion as the final form (the prompter, 2026-10-07)
           + [f"{SWARM_DIR}/SwarmPlan_tandava_sea_lion_{v}{pose}.json" for v in (1, 2, 3) for pose in ("", "_feed")])
for rel in RETIRED:
    g.stale += [rel, rel + ".meta"]

G_ASSET = {
    "TandavaSwarmFaunaConfig":  guid("asset/TandavaSwarmFaunaConfig"),
    "TandavaSwarmFauna.prefab": guid("asset/TandavaSwarmFauna.prefab"),
    "SwarmSpecies":             guid("asset/TandavaSwarmSpecies"),
    "FloraMass":                guid("asset/TandavaFloraMass"),   # the Borromean Mass fork (its guid kept)
    "Membrane.prefab":          guid("asset/TandavaMembrane.prefab"),
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
EXISTING["CapsuleMembrane"] = lib.existing_guid("Assets/_Prefabs/Environment/CapsuleMembrane.prefab")
EXISTING["Nucleus"] = lib.CELL_VISUALS["NucleusPrefab"]
EXISTING["Cytoplasm"] = lib.CELL_VISUALS["CytoplasmPrefab"]
EXISTING["CellIcon"] = lib.CELL_VISUALS["CellIcon"]
EXISTING["ExtraOmniCrystals"] = "daa37ae0e7af4b04383c1c4e6e76817d"   # the Swarm cell's modifier
ELEMENT_NAME = {1: "Mass", 2: "Space"}   # research index -> the canonical config's element name
EXISTING["HalfNucleus"] = lib.existing_guid("Assets/_Prefabs/Environment/HalfNucleus.prefab")   # the Scurry cell's core
CAPSULE_MEMBRANE = "Assets/_Prefabs/Environment/CapsuleMembrane.prefab"
MEMBRANE_PREFAB = "Assets/_Prefabs/Environment/TandavaMembrane.prefab"

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
# THE NUMBERS (the harness's - TandavaHarness.cs; every one is read back below)
# ═════════════════════════════════════════════════════════════════════════════
# The prompter's third pass (2026-10-06): "make the cell smaller, its too big; more types of floras, cramped up; an
# environment, from Scurry intensity 4". So: the CapsuleMembrane at two thirds, the Scurry cell's half-size core and a reef of
# seven flora forks packed through the band. (The environment it asked for - Atlantis, thinned - came out again at its next
# word, "remove the environment": the cell holds only the reef, the creature and the pilots.)
MEMBRANE_RADIUS = 800.0          # TandavaMembrane (CapsuleMembrane, radius 800): CLOSED - the membrane is the creature's wall
NUCLEUS_RADIUS = 196.0           # HalfNucleus, the Scurry cell's core: fauna eat nothing inside it
BODY_REACH = 160.0               # the longest body's half-length: its centre roams the membrane less this
ROAM_RADIUS = MEMBRANE_RADIUS * 0.97 - BODY_REACH   # the swarm's own member clamp is 0.97 of the membrane
HATCH = (-430.0, 0.0, 0.0)       # it hatches whole, facing +x, across the cell from the pilots
PLANT_INNER, PLANT_OUTER = 240.0, 720.0    # the flora band: clear of the nucleus, inside the wall
# the reef: (species, element as a research index, how many, prisms a plant may grow). The Mass species carry the food
# (each plant 2,000-4,400 volume - about half a meal or most of one); the Space ones are colour and a snack. Every row is
# the harness's TandavaArena.Flora (read back below), its leaf volume read off the canonical config
FLORA_TABLE = [("Borromean", 1, 5, 60), ("Coral", 1, 5, 110), ("Lantern", 1, 4, 70), ("Reed", 1, 4, 130),
               ("Frond", 1, 3, 120), ("Tendril", 2, 3, 84), ("Coral", 2, 3, 140)]

# The swarm (TandavaSwarmFaunaConfig)
DENSITY = 3                      # PlanDensity: every plan unit is three tadpoles in game
SEED_MEMBERS = 180               # x DENSITY, capped at the seeded form's plan: it hatches WHOLE (no young serpent)
BITERS_PER_STEP = 8              # the harness's 2026-10-06 sweep: 2 left a feeding head unasked for whole meals
CRUISE = 3.0                     # voxels/step: 3 x UnitScale 2 x 10 Hz = 60 u/s calm; x the director's mood levers
TURN_PER_STEP = 0.03
SORT_TURN_CARRY = 1.0            # the body turns as one piece (harness T3: 93% of its shape round a fleeing turn, 18% without)
# a form change READS in about a second (harness T16: 1.0 s to 60% coverage, 3.5 s at the research's member speeds)
SORT_VMAX_SCALE, SORT_WELL_CLIP = 3.0, 1.0
SORT_LAY_RATE, SORT_LAY_MAX = 0.084, 8
KILL_LAY_HOLD_SECONDS = 0.0      # a cut never stops it regrowing - only feeding does (the director's hold)
SORT_LAY_RAMP_SECONDS = 1.5      # a cut limb is back in about a second (harness T4)
UNIT_SCALE, PRISM_SCALE = 2.0, 1.0
EGG = [20.45, 40.31, 22.18, 12.8]  # Charge, Mass, Space, Time (the Swarm cell's measured egg volumes)
STOMACH_EGGS = 500
BODY_FILL = 0.939                # SortBodyFill (the sort core's grown body)
MAX_PROXIES = 160
# the metabolism: starvation is the swarm's OWN (SwarmFauna.Starving: unfed this long while hungry, it sheds one member
# per interval). The sort config's 90 s / 1 s is a reef's pace; a hunt is decided in minutes
STARVATION_SECONDS, SHED_INTERVAL_SECONDS, FORAGE_BELOW = 30, 0.25, 0.5
# SortWellsPerType 24 / SortWellDead 0: the dance is a STROKE figure - 12 Gaussian wells a type blur a stroke into a blob
SORT_WELLS_PER_TYPE, SORT_WELL_DEAD = 24, 0
# MultiDomain OFF: a lineage swarm gives its slots the OTHER playable domains, and with every pilot on one domain one of
# them IS the pilots' - an enemy that reads as a friend
MULTI_DOMAIN = 0
# MacroLod OFF: a collapsed swarm stops its worker tick, and the director runs on published ticks
MACRO_LOD = 0

# The economy (TandavaHarness.BuildForms)
FILL_TO_EVOLVE = 0.9
BANK_SHARE = [0.15, 0.28, 0.0, 0.4]   # of the stomach, RISING: a carried-over stomach never skips a form; low: the forms come fast
MEAL_VOLUME = 4000.0

# The halo and the end conditions
HALO_COUNT, HALO_TO_BREAK = 12, 9      # EndConditionOverridesSO.DefaultTandavaHaloRingsToBreak (asserted)
HALO_MOUTH = 24.0                      # world: rings sit ~87 u apart on the halo
GUARD_RADIUS, GUARD_MEMBERS = 40.0, 6  # a pack is 7 Time units x density 3; a lobby that culls most of it unguards it
HALO_POINTS = 25                       # TandavaScoringRuleSO.haloPoints: a ring is worth a pack of culls
BREAK_PERCENT = 35                     # EndConditionOverridesSO.DefaultTandavaBreakPercent (asserted): the shatter

# Pilots line up across the cell from the hatch, facing it (Y -90 deg)
SPAWN_X = 600.0
SPAWN_Z = [-90.0, -30.0, 30.0, 90.0]
SPAWN_ROT = (0.0, -0.7071068, 0.0, 0.7071068)

# The phase ladder (the Swarm cell's ratios against this cell's modelled mature mass)
RESTLESS_ENTER, RESTLESS_EXIT, FRENZY_ENTER, FRENZY_EXIT = 0.35, 0.26, 2.5, 2.2
COLLIDER_CEILING = 1200


# ── Palettes (HDR: above 1 glows). The cell keeps its OWN colours for every form; it glows GOLD from the rise into the
# Lord of the Dance until the dance ends - gold, never fire (the prompter took fire out of the mode, 2026-10-06)
def pal(bright, dull, edge, straight, cyto):
    return dict(MembraneBright=bright, MembraneDull=dull, NucleusEdge=edge, NucleusStraight=straight, Cytoplasm=cyto)


ASCENSION_PALETTE = pal((1.40, 1.12, 0.42, 1), (0.16, 0.11, 0.02, 1), (1.00, 0.80, 0.30, 0.45), (1.15, 0.96, 0.52, 0.45),
                        (0.86, 0.72, 0.36, 1))

FORMS = [
    dict(name="Great Serpent", role=0,
         line="The {0} is hungry. Wherever it eats, its hood comes off its head to guard its mouth."),
    dict(name="Many-Headed Serpent", role=0,
         line="Its coils split: a {0}. Every head dips to the food when it eats."),
    dict(name="Lord of the Dance", role=1, line=""),   # the rise is RisingLine; the halo lighting is HaloLitLine
    dict(name="Antlion", role=2,
         line="The drum stops. The {0} drops out of the halo, jaws open. One last feast and the cycle is complete."),
]

# ═════════════════════════════════════════════════════════════════════════════
# Derived
# ═════════════════════════════════════════════════════════════════════════════
PLANS = tandava_plans.bake_all()
plan_errors = tandava_plans.validate(PLANS)
STOMACH_CAPACITY = STOMACH_EGGS * DENSITY * sum(EGG) * 0.25   # SwarmFauna.StomachCapacityVolume
# plan voxels -> world: SwarmPlanData.Upsample scales positions by cbrt(m), and the unit scale
WORLD = DENSITY ** (1.0 / 3.0) * UNIT_SCALE


def leaf_volume(path):
    lx, ly, lz = map(float, re.search(r"LeafSize: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+)\}", g.read(path)).groups())
    return lx * ly * lz


def canon_flora(species, element):
    return f"Assets/_SO_Assets/Lifeforms/{species} Flora {ELEMENT_NAME[element]}.asset"


FLORA = [dict(species=sp, element=el, count=n, prisms=pr, path=canon_flora(sp, el), leaf=leaf_volume(canon_flora(sp, el)),
              name=f"Tandava {sp} Flora {ELEMENT_NAME[el]} Config Data",
              key="FloraMass" if (sp, el) == ("Borromean", 1) else f"Flora{sp}{ELEMENT_NAME[el]}")
         for sp, el, n, pr in FLORA_TABLE]
for f in FLORA:
    EXISTING[f"Canon{f['key']}"] = lib.existing_guid(f["path"])
    G_ASSET.setdefault(f["key"], guid(f"asset/Tandava{f['key']}"))
MASS_LEAF = leaf_volume(canon_flora("Borromean", 1))   # the food survey's fallback when a plant reports no leaf


def variants_of(form):
    """(key, display name) of each variant of a form, in the plans' order (tandava_plans.VARIANTS)."""
    return [(key, name) for key, f, name, _, _ in tandava_plans.VARIANTS if f == form]


def wv(p):
    return [x * WORLD for x in p]


def plan_reach(plan):
    """The farthest any of a plan's units comes from its centre, in any frame (world) - TandavaHarness.Load's Reach."""
    q = plan["pos"]
    return max(math.sqrt(q[i] * q[i] + q[i + 1] * q[i + 1] + q[i + 2] * q[i + 2]) for i in range(0, len(q), 3)) * WORLD


def build_forms():
    """The four forms as TandavaHarness.BuildForms runs them: each variant's plan, strike pose and meal coils by index
    into the swarm config's ScriptedPlans, its mouths (travel, strike, each coil's; world, body axes), how far out it may
    roll up (the wall less its coils' reach), and - the dance - its halo's centre."""
    out = []
    for k, f in enumerate(FORMS):
        vs = []
        for key, name in variants_of(k):
            p = PLANS[key]
            feeds = key + "_feed" in PLANS
            coils = [f"{key}_{c}" for c in tandava_plans.coils_of(k)]
            lunge = [f"{key}_{c}" for c in tandava_plans.lunges_of(k)]
            vs.append(dict(name=name, plan=PLAN_KEYS.index(key), feed=PLAN_KEYS.index(key + "_feed") if feeds else -1,
                           coils=[PLAN_KEYS.index(c) for c in coils], coil_mouths=[wv(PLANS[c]["mouth"]) for c in coils],
                           coil_roam=MEMBRANE_RADIUS * 0.97 - max(plan_reach(PLANS[c]) for c in coils) if coils else 0.0,
                           lunge=PLAN_KEYS.index(lunge[0]) if lunge else -1,      # the charge
                           snap=PLAN_KEYS.index(lunge[1]) if lunge else -1,       # the strike, as it reaches the pilot
                           lunge_mouth=wv(PLANS[lunge[0]]["mouth"]) if lunge else [0.0, 0.0, 0.0],
                           mouth=wv(p["mouth"]) if "mouth" in p else [0.0, 0.0, 0.0],
                           feed_mouth=wv(PLANS[key + "_feed"]["mouth"]) if feeds else [0.0, 0.0, 0.0],
                           halo=wv(p["ring"]["centre"]) if "ring" in p else [0.0, 0.0, 0.0],
                           n=p["n"] * DENSITY))
        out.append(dict(f, bank=BANK_SHARE[k], meal=MEAL_VOLUME if f["role"] != 1 else 1.0, variants=vs))
    return out


FORM_ROWS = build_forms()
_ring = next(PLANS[k]["ring"] for k in PLAN_KEYS if "ring" in PLANS[k])
HALO_RADIUS, GUARD_POST_RADIUS = _ring["radius"] * WORLD, _ring["guardOrbit"] * WORLD
HALO_OFFSET = max(math.sqrt(sum(x * x for x in v["halo"])) for v in FORM_ROWS[2]["variants"])
DANCE_REACH_NEEDED = GUARD_POST_RADIUS + GUARD_RADIUS + HALO_OFFSET

PLANT_VOLUME = max(f["prisms"] * f["leaf"] for f in FLORA)    # the biggest plant: a meal is most of one
PLANTS = sum(f["count"] for f in FLORA)
FOREST_PRISMS = sum(f["count"] * f["prisms"] for f in FLORA)
FOREST_VOLUME = sum(f["count"] * f["prisms"] * f["leaf"] for f in FLORA)


def body_volume(plan):
    m3 = (2.0 * UNIT_SCALE * PRISM_SCALE) ** 3          # SwarmTickJob: scale = 2 x unit x prismScale x half
    h = plan["half"]
    return DENSITY * BODY_FILL * sum(m3 * abs(h[3 * k] * h[3 * k + 1] * h[3 * k + 2]) for k in range(plan["n"]))


BIGGEST_BODY = max(body_volume(PLANS[k]) for k in PLAN_KEYS)


def ladder():
    rt = lambda x, st: int(math.ceil(x / st) * st)
    prisms = FOREST_PRISMS + MAX_PROXIES
    volume = FOREST_VOLUME + BIGGEST_BODY
    return {
        "RestlessEnter": rt(prisms * RESTLESS_ENTER, 100), "RestlessExit": rt(prisms * RESTLESS_EXIT, 100),
        "FrenzyEnter": rt(prisms * FRENZY_ENTER, 100), "FrenzyExit": rt(prisms * FRENZY_EXIT, 100),
        "RestlessEnterVolume": rt(volume * RESTLESS_ENTER, 1000),
        "RestlessExitVolume": rt(volume * RESTLESS_EXIT, 1000),
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


def v3(x, y=0.0, z=0.0):
    return f"{{x: {num(round(float(x), 3))}, y: {num(round(float(y), 3))}, z: {num(round(float(z), 3))}}}"


# ── the director's dials: TandavaDirectorSettings' C# defaults, read off the source (the ONE place a number lives)
DIRECTOR_SRC = g.read(SCRIPT_PATHS["TandavaDirectorCore"])


def director_defaults():
    body = DIRECTOR_SRC.split("public sealed class TandavaDirectorSettings")[1].split("\n    }\n")[0]
    out = []
    for m in re.finditer(r"(?m)^\s*(\[NonSerialized\]\s*)?public (float|int) ([^;]+);", body):
        if m.group(1):
            continue
        for decl in m.group(3).split(","):
            name, value = (x.strip() for x in decl.split("="))
            out.append((name, float(value.rstrip("f")), m.group(2)))
    return out


DIRECTOR = director_defaults()


def director_min_food():
    return next(v for n, v, _ in DIRECTOR if n == "MinFood")
DIRECTOR_AUTHORED = {"RoamRadius": ROAM_RADIUS, "HaloCount": HALO_COUNT, "HaloToBreak": HALO_TO_BREAK}

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
for k in PLAN_KEYS:
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
                   ("TurnPerStep", num(TURN_PER_STEP)),
                   ("UnitScale", num(UNIT_SCALE)), ("PrismScale", num(PRISM_SCALE)), ("StomachEggs", STOMACH_EGGS),
                   ("StarvationSeconds", num(STARVATION_SECONDS)), ("ShedIntervalSeconds", num(SHED_INTERVAL_SECONDS)),
                   ("ForageBelow", num(FORAGE_BELOW)), ("SortWellsPerType", SORT_WELLS_PER_TYPE),
                   ("SortWellDead", num(SORT_WELL_DEAD)), ("SortLayRate", num(SORT_LAY_RATE)), ("SortLayMax", SORT_LAY_MAX),
                   ("KillLayHoldSeconds", num(KILL_LAY_HOLD_SECONDS)), ("SortLayRampSeconds", num(SORT_LAY_RAMP_SECONDS)),
                   ("SortBodyFill", num(BODY_FILL)), ("SortWellClip", num(SORT_WELL_CLIP)),
                   ("EggVolume", "{x: %s, y: %s, z: %s, w: %s}" % tuple(num(e) for e in EGG))):
    cfg = set_key(cfg, key, value)
# keys the shipped sort config predates (Unity has been reading their C# defaults): written explicitly here, in
# declaration order, because each is a Tandava decision
for key in ("ScriptedPlans", "ScriptedPlanPeriods", "ScriptedPlanWellClip", "SortTurnCarry", "SortVMaxScale", "MaxHitMaterialisationsPerFrame",
            "UnifiedPrismBodies", "MacroLod", "Bestiary", "HuntEnter", "LurkCalm", "LocustPhaseSeconds"):
    assert not re.search(rf"(?m)^  {key}:", cfg), f"the shipped sort config now carries '{key}' - set it, do not insert it"
cfg = insert_after(cfg, "TimePlan",
                   "  ScriptedPlans:\n" +
                   "".join(f"  - {{fileID: 4900000, guid: {G_PLAN[k]}, type: 3}}\n" for k in PLAN_KEYS) +
                   "  ScriptedPlanPeriods:\n" +
                   "".join(f"  - {PLANS[k]['frameSteps']}\n" for k in PLAN_KEYS) +
                   "  ScriptedPlanWellClip:\n" +
                   "".join(f"  - {num(PLANS[k].get('wellClip', 1.0))}\n" for k in PLAN_KEYS))
cfg = insert_after(cfg, "TurnPerStep", f"  SortTurnCarry: {num(SORT_TURN_CARRY)}\n  SortVMaxScale: {num(SORT_VMAX_SCALE)}\n")
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


def flora_fork(f):
    """A FORK of a canonical species config: the species' own prefab, plate, quota, heart and shield byte for byte, with
    only what a CELL owns rewritten - the name, how many, where (DISPERSED: the whole band between the nucleus and the
    wall, spread apart, no pens) and how big a plant may get here. A canonical config that predates the population keys
    (every species but Borromean) gets them written after NewPlantPeriod, where FloraConfigurationSO declares them."""
    t = g.read(f["path"])
    t = re.sub(r"(?m)^  m_Name: .*$", f"  m_Name: {f['name']}", t, count=1)
    t, n = re.subn(r"(?m)^  InitialSpawnCount: \d+$", f"  InitialSpawnCount: {f['count']}", t, count=1)
    assert n == 1, f"{f['name']}: InitialSpawnCount not found in the canonical config"
    for key, after in (("PopulationSize", "NewPlantPeriod"), ("MaxLivePopulation", "PopulationSize")):
        t, n = re.subn(rf"(?m)^  {key}: \d+$", f"  {key}: {f['count']}", t, count=1)
        if n == 0:
            t = insert_after(t, after, f"  {key}: {f['count']}\n", label=f["name"])
    t = re.sub(r"(?m)^  (PlantRadiusCellFraction(Max|Min)Override|MaxTotalSpawnedObjectsOverride|SpreadPlanting): .*\n", "", t)
    t = re.sub(r"(?m)^  PlantingPens:.*\n(?:  - .*\n|    .*\n)*", "", t)
    if not t.endswith("\n"):
        t += "\n"
    t += (f"  PlantRadiusCellFractionMaxOverride: {g5(PLANT_OUTER / MEMBRANE_RADIUS)}\n"
          f"  PlantRadiusCellFractionMinOverride: {g5(PLANT_INNER / MEMBRANE_RADIUS)}\n"
          f"  MaxTotalSpawnedObjectsOverride: {f['prisms']}\n"
          "  SpreadPlanting: 1\n")
    return t


for f in FLORA:
    g.emit_asset(f"{CELL_DIR}/{f['name']}.asset", G_ASSET[f["key"]], flora_fork(f))


# the cell's wall: a generated COPY of the shipped prefab with only Tandava's fields rewritten, re-derived
# from the source every run (the Regatta precedent: a generator-owned environment prefab per cell)
def prefab_copy(src, name, fields, label):
    t = g.read(src)
    t, n = re.subn(r"(?m)^  m_Name: (?!$).*$", f"  m_Name: {name}", t, count=1)
    assert n == 1, f"{label}: root name not found"
    for key, value in fields:
        t = set_key(t, key, value, label)
    return t


g.emit_prefab(MEMBRANE_PREFAB, G_ASSET["Membrane.prefab"],
              prefab_copy(CAPSULE_MEMBRANE, "TandavaMembrane",
                          # the baked animation is keyed by its radius: at 800 it would not match, so the membrane animates live
                          (("radius", num(MEMBRANE_RADIUS)), ("animationPreset", "{fileID: 0}")), "membrane"))
MEMBRANE_ROOT_FID = re.search(r"--- !u!1 &(\d+)\n", g.files[MEMBRANE_PREFAB]).group(1)

g.emit_asset(f"{CELL_DIR}/Tandava Cell Spawn Profile.asset", G_ASSET["SpawnProfile"],
             so(lib.SCRIPT_GUIDS["SpawnProfileSO"], "Tandava Cell Spawn Profile") +
             "  FloraExcludeLocalDomain: 0\n  FloraSpawnVolumeCeiling: 12000\n  FloraInitialDelaySeconds: 0\n"
             "  FloraSpawnIntervalSeconds: 0\n  FloraPopulationScale: 1\n  FloraPlantBudgetScale: 1\n"
             "  FloraPrismScale: 1\n  SupportedFloras:\n" +
             "".join(f"  - {{fileID: 11400000, guid: {G_ASSET[f['key']]}, type: 2}}\n" for f in FLORA) +
             "  FaunaExcludeLocalDomain: 0\n  InitialFaunaSpawnWaitTime: 6\n  InitialFaunaReleaseTier: 2147483647\n"
             "  FaunaSpawnVolumeThreshold: 1\n  FaunaPopulationScale: 1\n  BaseFaunaSpawnTime: 30\n"
             "  SeedFullWaveEveryTick: 0\n  FaunaFoodFloor: 0\n  FaunaInitialDelaySeconds: 0\n"
             "  FaunaSpawnIntervalSeconds: 0\n  HerbivoreSpawnPointCount: 3\n  HerbivoreSpawnRadius: 500\n"
             "  PredatorSpawnPointCount: 0\n  PredatorSpawnRadius: 900\n  SupportedFaunas:\n"
             f"  - {{fileID: 11400000, guid: {G_ASSET['SwarmSpecies']}, type: 2}}\n")

LADDER = ladder()
g.emit_asset(f"{CELL_DIR}/Tandava Cell Config.asset", G_ASSET["CellConfig"],
             so(lib.SCRIPT_GUIDS["CellConfigDataSO"], "Tandava Cell Config") +
             f"""  CellName: Tandava
  Description: A small closed cell for one creature's hunt - a membrane two thirds the standard, the Scurry
    cell's half-size core and a cramped reef of seven flora forks; no environment. Authored by
    Tools/Build/author_tandava_assets.py
  Icon: {{fileID: 21300000, guid: {EXISTING['CellIcon']}, type: 3}}
  Difficulty: 2
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE_ROOT_FID}, guid: {G_ASSET['Membrane.prefab']}, type: 3}}
  NucleusPrefab: {{fileID: 7555898194514117247, guid: {EXISTING['HalfNucleus']}, type: 3}}
  CytoplasmPrefab: {{fileID: 639495419069806261, guid: {EXISTING['Cytoplasm']}, type: 3}}
  CytoplasmShardDistance: 0
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
def palette_yaml(p, indent):
    return "".join(f"{indent}{k}: {colour(p[k])}\n"
                   for k in ("MembraneBright", "MembraneDull", "NucleusEdge", "NucleusStraight", "Cytoplasm"))


forms_yaml = ""
for f in FORM_ROWS:
    forms_yaml += (f"  - DisplayName: {yaml_str(f['name'])}\n    Role: {f['role']}\n    FillToEvolve: {num(FILL_TO_EVOLVE)}\n"
                   f"    BankShare: {num(f['bank'])}\n    MealVolume: {num(f['meal'])}\n"
                   f"    Line: {yaml_str(f['line']) if f['line'] else ''}\n    Variants:\n")
    for v in f["variants"]:
        forms_yaml += (f"    - DisplayName: {yaml_str(v['name'])}\n      PlanIndex: {v['plan']}\n      FeedPlanIndex: {v['feed']}\n"
                       + ("      CoilPlanIndices:\n" + "".join(f"      - {c}\n" for c in v["coils"]) if v["coils"] else "      CoilPlanIndices: []\n")
                       + ("      CoilMouths:\n" + "".join(f"      - {v3(*m)}\n" for m in v["coil_mouths"]) if v["coil_mouths"] else "      CoilMouths: []\n")
                       + f"      CoilRoamRadius: {num(round(v['coil_roam'], 1))}\n"
                       + f"      LungePlanIndex: {v['lunge']}\n      SnapPlanIndex: {v['snap']}\n"
                       + f"      LungeMouth: {v3(*v['lunge_mouth'])}\n"
                       f"      Mouth: {v3(*v['mouth'])}\n      FeedMouth: {v3(*v['feed_mouth'])}\n      HaloCentre: {v3(*v['halo'])}\n")

director_yaml = "  Director:\n" + "".join(
    f"    {name}: {num(round(DIRECTOR_AUTHORED.get(name, value), 3)) if kind == 'float' else int(DIRECTOR_AUTHORED.get(name, value))}\n"
    for name, value, kind in DIRECTOR)

SETTINGS_REL = "Assets/_SO_Assets/Games/TandavaSettings.asset"
g.emit_asset(SETTINGS_REL, G_ASSET["TandavaSettings"],
             so(G_SCRIPT["TandavaSettingsSO"], "TandavaSettings") +
             f"  SwarmConfig: {{fileID: 11400000, guid: {G_ASSET['TandavaSwarmFaunaConfig']}, type: 2}}\n"
             f"  HatchPoint: {v3(*HATCH)}\n  HatchHeading: {v3(1)}\n  Forms:\n" + forms_yaml + director_yaml +
             f"  FoodPerPrism: {num(round(MASS_LEAF, 3))}\n  FoodCheckSeconds: 0.5\n"
             f"  HaloRadius: {num(round(HALO_RADIUS, 2))}\n  HaloMouthRadius: {num(HALO_MOUTH)}\n"
             f"  GuardPostRadius: {num(round(GUARD_POST_RADIUS, 2))}\n  GuardRadius: {num(GUARD_RADIUS)}\n"
             f"  GuardMembers: {GUARD_MEMBERS}\n  GuardElement: 4\n  HaloGuardScale: 0.35\n"
             "  HaloBloomSeconds: 1.2\n  HaloBreakSeconds: 0.6\n  MaxPlausibleSpeed: 1500\n"
             "  FormBurstShards: 64\n  HaloBurstShards: 18\n  BurstSpeed: 70\n  BurstScale: 3\n"
             "  FlashSeconds: 0.6\n  FlashRadius: 260\n  FlashStrength: 0.8\n"
             "  AscensionPalette:\n" + palette_yaml(ASCENSION_PALETTE, "    ") +
             "  TransitionSeconds: 3\n  TransitionFlash: 1.2\n  RestoreSeconds: 5\n"
             "  StartLine: Something in the reef remembers the old shapes.\n"
             f"  FeedingLine: {yaml_str('It is feeding, rolled up round the plant - its guards circle it. Strike the body.')}\n"
             "  MealBrokenLine: Its meal is broken. It bolts.\n"
             "  RisingLine: It has eaten enough. It rises into the Lord of the Dance.\n"
             "  HaloLitLine: The halo is lit. Break the rings before the drum stops.\n"
             f"  FirstHaloLine: {yaml_str('A ring is broken. Break {1} and the dance falls.')}\n"
             f"  LastHaloLine: {yaml_str('{0} of {1}. One more ring!')}\n"
             "  CompletedLine: The Antlion has fed. The cycle is complete.\n"
             "  WonLine: Its body is broken. The reef keeps its turn.\n"
             "  DanceBrokenLine: The dance is broken. The reef keeps its turn.\n"
             f"  HeldOffLine: {yaml_str('Time. You held it off - the cycle is unfinished.')}\n"
             f"  LungeLine: {yaml_str('It turns on you - its guards are out round its jaws. Hurt it and it runs.')}\n"
             "  RoamingLabel: Roaming\n  WaryLabel: Wary\n  FleeingLabel: Fleeing\n"
             f"  LungingLabel: {yaml_str('Lunging - mind its guards')}\n"
             f"  FeedingLabel: {yaml_str('Feeding - strike the body')}\n  RisingLabel: Rising\n"
             f"  DancingLabel: {yaml_str('Dancing - break the halo')}\n  FeastLabel: the last feast\n"
             "  HaloLabel: Halo rings broken\n  DrumLabel: Drum\n  TimeLabel: Time left\n"
             f"  BodyFormat: {yaml_str('Body {0}%')}\n"
             "  NudgeThreshold: 25\n  NudgeFraction: 0.2\n  MaxNudge: 8\n  AiFlankBack: 70\n")

g.emit_asset("Assets/_SO_Assets/Scoring Rules/TandavaScoringRule.asset", G_ASSET["TandavaScoringRule"],
             so(G_SCRIPT["TandavaScoringRuleSO"], "TandavaScoringRule") +
             f"  metric: 7\n  golfRules: 0\n  haloPoints: {HALO_POINTS}\n")

CARD_HULLS = ("Rhino", "Squirrel", "Sparrow")
g.emit_asset("Assets/_SO_Assets/Games/ArcadeGameTandava.asset", G_ASSET["ArcadeGameTandava"],
             so(EXISTING["SO_ArcadeGame"], "ArcadeGameTandava") + f"""  Mode: {MODE_ID}
  IsMultiplayer: 1
  DisplayName: Tandava
  Description: One creature lives in this cell, and it is hungry. It hatches as a great serpent and
    eats wherever it likes - bolting when you charge it, putting its guards out round its mouth when
    it feeds. Fed, it becomes a many-headed serpent, then rises into the Lord of the Dance inside a
    halo, then an antlion. Shatter it or break its halo before its last feast - there is no clock. Everyone
    flies together.
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
  - It cannot heal while it eats. Strike its body then - not its head, where the guards are.
  - Hurt it badly enough at a plant and the meal is broken. Every broken meal is time it does not have.
  - A cut limb grows back from what it has eaten. Starve it and the cuts stay cut.
  - Healthy, it turns on a pilot who comes close and lunges, its guards out. Hurt it below seven tenths and it runs instead.
  - At the dance, break nine of the twelve halo rings before the drum stops. The attendant packs guard the rings they pass.
  - The goals on the top left show its form and how close it is to the next. The cell glows gold only for the dance.
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
             lib.toast(133, "Break its meals - strike the body while it eats", domain_names=0, idle=1, idle_seconds=40) +
             lib.toast(134, "{0}", domain_names=0) +
             lib.toast(135, "{0}", domain_names=0) +
             lib.toast(136, "{0}", domain_names=0) +
             lib.toast(137, "{0}", domain_names=0) +
             lib.toast(138, "{0}", domain_names=0))
g.register_toast_config(G_ASSET["GameToastConfigTandava"])


def fmt4(x):
    return f"{x:.4f}".rstrip("0").rstrip(".") or "0"


g.emit_asset("Assets/_SO_Assets/Mode Previews/ModePreview_Tandava.asset", G_ASSET["ModePreviewTandava"],
             so(EXISTING["ModePreviewDefinitionSO"], "ModePreview_Tandava") + f"""  Mode: {MODE_ID}
  Notes: 'The Tandava cell as the mode flies it - the dispersed flora, the creature, the pilots'' line.
    No director runs in a preview, so the swarm grazes as its first form rather than hunting, and the
    cell keeps its own colours. Vessel is -1 (ANY): the carousel''s pick flies it.'
  PreviewCell: {{fileID: 11400000, guid: {G_ASSET['CellConfig']}, type: 2}}
  PreviewCellsByIntensity: []
  StructurePrefab: {{fileID: 0}}
  TrackSpawnablesByIntensity: []
  Vessel: -1
  ObjectiveText: Shatter the creature or break its halo
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
    assert m, f"spawn transform {fid} not found"
    doc = m.group(0)
    new = re.sub(r"m_LocalRotation: \{[^}]*\}", "m_LocalRotation: {x: %s, y: %s, z: %s, w: %s}" % rot, doc)
    new = re.sub(r"m_LocalPosition: \{[^}]*\}", "m_LocalPosition: {x: %s, y: %s, z: %s}" % pos, new)
    new = re.sub(r"m_LocalEulerAnglesHint: \{[^}]*\}", "m_LocalEulerAnglesHint: {x: 0, y: -90, z: 0}", new)
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
    # a LINE across the cell from the hatch, not a ring round the nucleus
    scene = lib.replace_block(scene, "  arrangeSpawnPointsAroundCell: 1\n", "  arrangeSpawnPointsAroundCell: 0\n",
                              "spawn ring flag")
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
# the pilots' line is this script's, whichever scene it is (idempotent)
for fid, z in zip(SPAWN_FIDS, SPAWN_Z):
    SCENE = move_transform(SCENE, fid, (num(SPAWN_X), 0, num(z)), tuple(num(v) for v in SPAWN_ROT))
g.emit_scene("MinigameTandava", G_ASSET["MinigameTandava.unity"], SCENE)

# ═════════════════════════════════════════════════════════════════════════════
# 6. The shared registries
# ═════════════════════════════════════════════════════════════════════════════
g.register_arena_card(G_ASSET["ArcadeGameTandava"])        # master + ARENA grid, never Arcade
g.register_always_unlocked()
g.register_build_scene("MinigameBroodRush", "MinigameTandava", G_ASSET["MinigameTandava.unity"])
# the halo's target was the ring of fire's: migrate the key (FormerlySerializedAs reads the old one; this rewrites it)
_ec = g.read_current(lib.END_CONDITIONS)
_ec = re.sub(r"(?m)^  tandavaFlamesToBreak(Build)?: ", lambda m: f"  tandavaHaloRingsToBreak{m.group(1) or ''}: ", _ec)
g.emit(lib.END_CONDITIONS, _ec)
g.set_end_condition("tandavaBreakPercent", after="broadsidePointsPerPilot", value=BREAK_PERCENT)
g.set_end_condition("tandavaHaloRingsToBreak", after="tandavaBreakPercent", value=HALO_TO_BREAK)


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
            errs.append(f"spawn point {fid} is not on the pilots' line at x = {num(SPAWN_X)}, z = {num(z)}")
    return errs


errors = [f"plan: {e}" for e in plan_errors]
errors += validate_scene(g.files[SCENE_REL])

# the forms: four, in their roles, three variants each, the plan list in the config's order
if [f["role"] for f in FORM_ROWS] != [0, 0, 1, 2]:
    errors.append("the forms are not eater, eater, dance, final")
for f in FORM_ROWS:
    if len(f["variants"]) != 3:
        errors.append(f"{f['name']}: {len(f['variants'])} variants (the design: three, one drawn per match)")
    for v in f["variants"]:
        if (v["feed"] >= 0) != (f["role"] != 1):
            errors.append(f"{v['name']}: a form that eats needs a strike pose and only those do")
        if (len(v["coils"]) == 3) != (f["name"] in ("Great Serpent", "Many-Headed Serpent")) or len(v["coil_mouths"]) != len(v["coils"]):
            errors.append(f"{v['name']}: the two serpents (and only they) roll up to eat, in three formations, each with its mouth")
        if (v["lunge"] >= 0) != (f["name"] in ("Many-Headed Serpent", "Antlion")) or (v["snap"] >= 0) != (v["lunge"] >= 0):
            errors.append(f"{v['name']}: the Many-Headed Serpent and the Antlion (and only they) lunge in their own poses - "
                          "a charge (heads reared / jaws wide) and a strike")
        if v["coils"] and not ROAM_RADIUS <= v["coil_roam"] < MEMBRANE_RADIUS * 0.97:
            errors.append(f"{v['name']}: it may roll up {v['coil_roam']:.0f} u out - not between the swimming body's {ROAM_RADIUS:.0f} "
                          f"and the wall's {MEMBRANE_RADIUS * 0.97:.0f}")
eats = [f for f in FORM_ROWS if f["role"] != 1]
if [f["bank"] for f in eats] != sorted(f["bank"] for f in eats) or len({f["bank"] for f in eats}) != len(eats):
    errors.append(f"the banks {[f['bank'] for f in eats]} do not RISE form by form")
if max(BANK_SHARE) >= 0.97:
    errors.append("a bank at or above the stomach fill at which a meal ends full could never be reached")
if MEAL_VOLUME > PLANT_VOLUME:
    errors.append(f"a meal ({MEAL_VOLUME:.0f}) is bigger than the biggest plant ({PLANT_VOLUME:.0f})")
for f in FLORA:
    cap = int(re.search(r"MaxTotalSpawnedObjects: (\d+)", g.read(f["path"])).group(1))
    if f["prisms"] > cap:
        errors.append(f"{f['name']}: {f['prisms']} prisms a plant is past the species' own {cap}")
    if f["prisms"] * f["leaf"] < director_min_food():
        errors.append(f"{f['name']}: a whole plant ({f['prisms'] * f['leaf']:.0f}) is under MinFood - the creature never eats it")
if len({f["species"] for f in FLORA}) < 5:
    errors.append("fewer than five flora species: the reef is not varied")
# no environment: the prompter removed it (2026-10-06) - the cell holds the reef, the creature and the pilots
if "{fileID: 0}" not in g.files[f"{CELL_DIR}/Tandava Cell Config.asset"].split("EnvironmentPrefab:")[1].split("\n")[0]:
    errors.append("the cell carries an environment")
if f"radius: {num(MEMBRANE_RADIUS)}" not in g.files[MEMBRANE_PREFAB]:
    errors.append("the membrane prefab is not the authored radius")
if max(v["n"] for f in FORM_ROWS for v in f["variants"]) > 1000:
    errors.append("a form over 1,000 tadpoles (round 7's per-swarm ceiling)")

# the arena: the hatch, the flora band and the pilots' line inside the wall and clear of the nucleus
hatch_r = math.hypot(*HATCH)
if not NUCLEUS_RADIUS < hatch_r < ROAM_RADIUS:
    errors.append(f"the hatch (r {hatch_r:.0f}) is not between the nucleus and the roam radius {ROAM_RADIUS:.0f}")
if not NUCLEUS_RADIUS < PLANT_INNER < PLANT_OUTER < MEMBRANE_RADIUS * 0.97:
    errors.append("the flora band is not between the nucleus and the wall")
for z in SPAWN_Z:
    if math.hypot(SPAWN_X, z) >= MEMBRANE_RADIUS * 0.9:
        errors.append(f"a pilot spawn ({SPAWN_X}, 0, {z}) is outside the cell")
if len(SPAWN_Z) < len(CARD_HULLS):
    errors.append(f"{len(SPAWN_Z)} spawn points for {len(CARD_HULLS)} seats - GetRandomSpawnPose would stack pilots")

# the halo: rings that do not overlap, posts outside the halo, and a dance that fits inside the cell
spacing = 2 * math.pi * HALO_RADIUS / HALO_COUNT
if 2 * HALO_MOUTH >= spacing:
    errors.append(f"halo ring mouths ({2 * HALO_MOUTH:.0f}) overlap at {spacing:.0f} u spacing on a {HALO_RADIUS:.0f} u halo")
if not HALO_RADIUS < GUARD_POST_RADIUS:
    errors.append("the attendants' guard posts are not outside the halo")
if not 1 <= HALO_TO_BREAK <= HALO_COUNT:
    errors.append(f"{HALO_TO_BREAK} rings to break a halo of {HALO_COUNT}")
director = {name: value for name, value, _ in DIRECTOR}
if director["DanceReach"] < DANCE_REACH_NEEDED:
    errors.append(f"DanceReach {director['DanceReach']:.0f} is short of the halo's reach {DANCE_REACH_NEEDED:.0f} (posts + guards + offset)")
if director["DanceReach"] >= ROAM_RADIUS:
    errors.append("the dance's reach does not fit inside the roam radius")
if director["HaloCount"] != HALO_COUNT:
    errors.append(f"TandavaDirectorSettings.HaloCount {director['HaloCount']:.0f} != the authored halo {HALO_COUNT}")

# the collider budget: always-on plant hearts + one swarm fully engaged (heart + body per proxy)
colliders = PLANTS + 2 * MAX_PROXIES
if colliders >= COLLIDER_CEILING:
    errors.append(f"{colliders} colliders >= the ceiling {COLLIDER_CEILING}")

# the C# this script authors keys for
SO_SRC = {
    "SwarmFaunaConfigSO": g.read(f"{SWARM_SCRIPTS}/SwarmFaunaConfigSO.cs"),
    "CellConfigDataSO": g.read("Assets/_Scripts/Utility/DataContainers/CellConfigDataSO.cs"),
    "FloraConfigurationSO": g.read("Assets/_Scripts/Utility/DataContainers/FloraConfigurationSO.cs"),
    "TandavaSettingsSO": g.read(SCRIPT_PATHS["TandavaSettingsSO"]),
    "TandavaScoringRuleSO": g.read(SCRIPT_PATHS["TandavaScoringRuleSO"]),
}
for so_name, keys in (("SwarmFaunaConfigSO", ("ScriptedPlans", "ScriptedPlanPeriods", "ScriptedPlanWellClip", "MacroLod", "MultiDomain", "PlanDensity",
                                              "SortTurnCarry", "SortVMaxScale", "SortWellClip", "KillLayHoldSeconds",
                                              "SortLayRampSeconds", "SortLayMax")),
                      ("CellConfigDataSO", ("CytoplasmShardDistance", "EnvironmentPrefab", "EnvironmentIntensity")),
                      ("FloraConfigurationSO", ("MaxTotalSpawnedObjectsOverride", "SpreadPlanting")),
                      ("TandavaSettingsSO", ("SwarmConfig", "Forms", "Director", "AscensionPalette", "MaxNudge", "FoodPerPrism")),
                      ("TandavaScoringRuleSO", ("haloPoints",))):
    for key in keys:
        if not re.search(rf"\b{key}\b\s*(=|;)", SO_SRC[so_name]):
            errors.append(f"{so_name} no longer declares '{key}' - the key this script authors")
settings_keys = set(re.findall(r"(?m)^  (\w+):", g.files[SETTINGS_REL])) - {
    "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Enabled",
    "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier"}
declared = set(re.findall(r"public\s+[\w<>\[\]]+\s+(\w+)\s*(?:=|;)", SO_SRC["TandavaSettingsSO"].split("class TandavaSettingsSO")[1]))
if settings_keys != declared:
    errors.append(f"TandavaSettings.asset keys differ from TandavaSettingsSO: extra {sorted(settings_keys - declared)}, "
                  f"missing {sorted(declared - settings_keys)}")
director_keys = re.findall(r"(?m)^    (\w+): ", g.files[SETTINGS_REL].split("  Director:\n")[1].split("\n  FoodPerPrism")[0] + "\n")
if director_keys != [name for name, _, _ in DIRECTOR]:
    errors.append("the settings asset's Director block is not TandavaDirectorSettings' fields in declaration order")
for spec_struct, keys in (("TandavaFormSpec", ("DisplayName", "Role", "FillToEvolve", "BankShare", "MealVolume", "Line", "Variants")),
                          ("TandavaVariantSpec", ("DisplayName", "PlanIndex", "FeedPlanIndex", "CoilPlanIndices", "CoilMouths",
                                                  "CoilRoamRadius", "LungePlanIndex", "SnapPlanIndex", "LungeMouth", "Mouth", "FeedMouth",
                                                  "HaloCentre"))):
    body = SO_SRC["TandavaSettingsSO"].split(f"public struct {spec_struct}")[1].split("\n    }")[0]
    if re.findall(r"public\s+[\w<>\[\]]+\s+(\w+)\s*;", body) != list(keys):
        errors.append(f"{spec_struct}'s fields are not {keys} in order (the asset this script writes)")

# the harness models the swarm and the cell this script lays (TandavaHarness.cs) - read back, not trusted
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
        ("StomachEggs", r"const int StomachEggs = (\d+);", STOMACH_EGGS),
        ("Cruise", r"const float Cruise = ([\d.]+)f;", CRUISE),
        ("TurnPerStep", r"const float TurnPerStep = ([\d.]+)f;", TURN_PER_STEP),
        ("SortTurnCarry", r"const float SortTurnCarry = ([\d.]+)f;", SORT_TURN_CARRY),
        ("SortLayRate", r"const float SortLayRate = ([\d.]+)f;", SORT_LAY_RATE),
        ("SortLayMax", r"const int SortLayMax = (\d+);", SORT_LAY_MAX),
        ("KillLayHoldSeconds", r"const float KillLayHoldSeconds = ([\d.]+)f;", KILL_LAY_HOLD_SECONDS),
        ("SortLayRampSeconds", r"const float SortLayRampSeconds = ([\d.]+)f;", SORT_LAY_RAMP_SECONDS),
        ("BitersPerStep", r"const int BitersPerStep = (\d+);", BITERS_PER_STEP),
        ("SortWellsPerType", r"const int SortWellsPerType = (\d+);", SORT_WELLS_PER_TYPE),
        ("SortWellDead", r"const float SortWellDead = ([\d.]+)f;", SORT_WELL_DEAD),
        ("StarvationSeconds", r"StarvationSeconds = ([\d.]+)f", STARVATION_SECONDS),
        ("ShedIntervalSeconds", r"ShedIntervalSeconds = ([\d.]+)f", SHED_INTERVAL_SECONDS),
        ("ForageBelow", r"ForageBelow = ([\d.]+)f", FORAGE_BELOW),
        ("BodyFill", r"const float BodyFill = ([\d.]+)f;", BODY_FILL),
        ("FillToEvolve", r"const float FillToEvolve = ([\d.]+)f;", FILL_TO_EVOLVE),
        ("MealVolume", r"const float MealVolume = ([\d.]+)f;", MEAL_VOLUME),
        ("SortVMaxScale", r"const float SortVMaxScale = ([\d.]+)f;", SORT_VMAX_SCALE),
        ("SortWellClip", r"const float SortWellClip = ([\d.]+)f;", SORT_WELL_CLIP),
        ("MembraneRadius", r"MembraneRadius = ([\d.]+)f;", MEMBRANE_RADIUS),
        ("NucleusRadius", r"NucleusRadius = ([\d.]+)f;", NUCLEUS_RADIUS),
        ("BodyReach", r"BodyReach = ([\d.]+)f;", BODY_REACH),
        ("PlantInner", r"PlantInner = ([\d.]+)f", PLANT_INNER), ("PlantOuter", r"PlantOuter = ([\d.]+)f;", PLANT_OUTER),
        ("HaloCount", r"HaloCount = (\d+)", HALO_COUNT), ("HaloToBreak", r"HaloToBreak = (\d+)", HALO_TO_BREAK),
        ("GuardMembers", r"GuardMembers = (\d+)", GUARD_MEMBERS), ("GuardRadius", r"GuardRadius = ([\d.]+)f;", GUARD_RADIUS)):
    v = h_const(pattern, label)
    if v is not None and abs(float(v) - float(ours)) > 1e-6:
        errors.append(f"harness {label} = {v}, this script authors {ours}")
v = h_const(r"Hatch = new\(([^)]*)\)", "Hatch")
if v is not None and [float(x.strip().rstrip("f")) for x in v.split(",")] != list(HATCH):
    errors.append(f"harness Hatch ({v}) != {HATCH}")
v = h_const(r"(?s)\(string Species, int Element, int Count, int Prisms, float Leaf\)\[\] Flora =\s*\{(.*?)\n    \};", "Flora")
if v is not None:
    rows = [(m[0], int(m[1]), int(m[2]), int(m[3]), float(m[4])) for m in
            re.findall(r'\("(\w+)", (\d+), (\d+), (\d+), ([\d.]+)f\)', v)]
    ours = [(f["species"], f["element"], f["count"], f["prisms"]) for f in FLORA]
    if [r[:4] for r in rows] != ours:
        errors.append(f"harness Flora {[r[:4] for r in rows]} != the authored reef {ours}")
    for r, f in zip(rows, FLORA):
        if abs(r[4] - f["leaf"]) > 0.01:
            errors.append(f"harness Flora {r[0]} leaf {r[4]} != the canonical config's {f['leaf']:.3f}")
v = h_const(r"static readonly float\[\] BankShare = \{([^}]*)\}", "BankShare")
if v is not None and [float(x.strip().rstrip("f")) for x in v.split(",")] != BANK_SHARE:
    errors.append(f"harness BankShare {{{v.strip()}}} != {BANK_SHARE}")
v = h_const(r"static readonly float\[\] EggVolume = \{([^}]*)\}", "EggVolume")
if v is not None and [float(x.strip().rstrip("f")) for x in v.split(",")] != EGG:
    errors.append(f"harness EggVolume {{{v.strip()}}} != {EGG}")
v = h_const(r"public static readonly string\[\] Keys =\s*\{([^}]*)\}", "Keys")
if v is not None and re.findall(r'"(\w+)"', v) != PLAN_KEYS:
    errors.append("harness Keys are not tandava_plans.plan_keys() in order (the swarm config's ScriptedPlans)")

# the end conditions, the enum ids, the toast situations
eco = g.read("Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs")
m = re.search(r"DefaultTandavaBreakPercent = (\d+);", eco)
if not m or int(m.group(1)) != BREAK_PERCENT:
    errors.append("EndConditionOverridesSO.DefaultTandavaBreakPercent differs from the authored shatter percent")
m = re.search(r"DefaultTandavaHaloRingsToBreak = (\d+);", eco)
if not m or int(m.group(1)) != HALO_TO_BREAK:
    errors.append("EndConditionOverridesSO.DefaultTandavaHaloRingsToBreak differs from the authored rings to break")
if "tandavaFlamesToBreak" in g.files[lib.END_CONDITIONS]:
    errors.append("EndConditionOverrides.asset still carries the ring of fire's key")
if not re.search(rf"\bTandava = {MODE_ID},", g.read("Assets/_Scripts/Data/Enums/GameModes.cs")):
    errors.append(f"GameModes.Tandava is not {MODE_ID}")
toasts_src = g.read("Assets/_Scripts/Data/Enums/GameToastSituation.cs")
for name, sid in (("TandavaMatchStart", 128), ("TandavaFormTaken", 129), ("TandavaCompleted", 130), ("TandavaBroken", 131),
                  ("TandavaFeeding", 132), ("TandavaDenyHint", 133), ("TandavaMealBroken", 134), ("TandavaRising", 135),
                  ("TandavaHaloLit", 136), ("TandavaHaloBroken", 137), ("TandavaLunge", 138)):
    if not re.search(rf"\b{name} = {sid},", toasts_src):
        errors.append(f"GameToastSituation.{name} is not {sid}")
if not re.search(r"\bHalo = 3,", g.read("Assets/_Scripts/Data/Enums/ToySwitchSignal.cs")):
    errors.append("ToySwitchSignal.Halo is not 3 (the halo rings' verb)")

# the card is an ARENA card, single domain, its hulls only
card = g.files["Assets/_SO_Assets/Games/ArcadeGameTandava.asset"]
for h in CARD_HULLS:
    if EXISTING[f"Vessel_{h}"] not in card:
        errors.append(f"card missing {h}")
if G_ASSET["ArcadeGameTandava"] not in g.files.get(lib.ARENA_GRID, ""):
    errors.append("card is not on the ARENA grid")
if G_ASSET["ArcadeGameTandava"] in g.read_current(lib.ARCADE_GRID):
    errors.append("card is on the ARCADE grid (arena cards never are)")
if re.search(r"(?i)\bfire\b|\bflame", card + g.files[SETTINGS_REL]):
    errors.append("the card or the settings still speak of fire (the prompter took fire out of the mode)")

# the swarm config really is scripted, sort, dense enough, and regrows the way the harness proved
sw = g.files[f"{SWARM_DIR}/TandavaSwarmFaunaConfig.asset"]
for key, want in (("Model", "2"), ("PlanDensity", str(DENSITY)), ("MacroLod", str(MACRO_LOD)), ("MultiDomain", str(MULTI_DOMAIN)),
                  ("BitersPerStep", str(BITERS_PER_STEP)), ("Cruise", num(CRUISE)), ("SortTurnCarry", num(SORT_TURN_CARRY)),
                  ("KillLayHoldSeconds", num(KILL_LAY_HOLD_SECONDS)), ("SortLayRampSeconds", num(SORT_LAY_RAMP_SECONDS)),
                  ("StomachEggs", str(STOMACH_EGGS)), ("SeedMembers", str(SEED_MEMBERS)),
                  ("StarvationSeconds", num(STARVATION_SECONDS)), ("ShedIntervalSeconds", num(SHED_INTERVAL_SECONDS)),
                  ("ForageBelow", num(FORAGE_BELOW)), ("SortWellsPerType", str(SORT_WELLS_PER_TYPE)),
                  ("SortWellDead", num(SORT_WELL_DEAD)), ("SortVMaxScale", num(SORT_VMAX_SCALE)),
                  ("SortWellClip", num(SORT_WELL_CLIP))):
    if not re.search(rf"(?m)^  {key}: {re.escape(want)}$", sw):
        errors.append(f"swarm config {key} is not {want}")
if [x for x in re.findall(r"guid: ([0-9a-f]{32}), type: 3\}", sw.split("ScriptedPlans:")[1].split("ScriptedPlanPeriods:")[0])] \
        != [G_PLAN[k] for k in PLAN_KEYS]:
    errors.append(f"swarm config's ScriptedPlans are not the {len(PLAN_KEYS)} plans in order")
periods = [int(x) for x in re.findall(r"(?m)^  - (\d+)$", sw.split("ScriptedPlanPeriods:\n")[1])[:len(PLAN_KEYS)]]
if periods != [PLANS[k]["frameSteps"] for k in PLAN_KEYS]:
    errors.append(f"swarm config's ScriptedPlanPeriods {periods} are not the plans' own frame steps")
clips = [float(x) for x in re.findall(r"(?m)^  - ([\d.]+)$", sw.split("ScriptedPlanWellClip:\n")[1])[:len(PLAN_KEYS)]]
if clips != [float(PLANS[k].get("wellClip", 1.0)) for k in PLAN_KEYS] or sum(c != 1.0 for c in clips) != 12:
    errors.append(f"swarm config's ScriptedPlanWellClip {clips} are not the plans' own (1, the twelve lunge poses more)")
if SEED_MEMBERS * DENSITY < max(v["n"] for v in FORM_ROWS[0]["variants"]):
    errors.append("the seed is smaller than a Great Serpent: it would hatch as a young serpent")

if "--self-test" in sys.argv:
    # NEGATIVE CONTROL: the scene checks must fire on the donor, every one of them
    donor_errs = validate_scene(g.read(DONOR_SCENE))
    expected = 5 + 5 + 1 + 1 + len(SPAWN_FIDS)
    print(f"self-test: the donor scene trips {len(donor_errs)} of {expected} scene checks")
    for e in donor_errs:
        print("   ", e)
    sys.exit(0 if len(donor_errs) == expected else 1)

# ── report ──
print("Tandava - the creature's forms (density %d, stomach %.0f, a meal %.0f, a plant %.0f):"
      % (DENSITY, STOMACH_CAPACITY, MEAL_VOLUME, PLANT_VOLUME))
for f in FORM_ROWS:
    print(f"  {f['name']:<20} bank {f['bank']:.2f} of the stomach ({f['bank'] * STOMACH_CAPACITY:>7,.0f})  variants: " +
          ", ".join(f"{v['name']} {v['n']}" for v in f["variants"]))
print(f"  reef: {PLANTS} plants of {len({f['species'] for f in FLORA})} species - " +
      ", ".join(f"{f['count']} {f['species']} {ELEMENT_NAME[f['element']]} ({f['prisms']} x {f['leaf']:.1f})" for f in FLORA))
print(f"  forest {FOREST_PRISMS} prisms, {FOREST_VOLUME:,.0f} volume; biggest body {BIGGEST_BODY:,.0f} volume; {colliders} colliders worst case")
print("  environment: none")
print("  ladder: " + ", ".join(f"{k} {v:,}" for k, v in LADDER.items()))
print(f"  arena: membrane {MEMBRANE_RADIUS:.0f}, roam {ROAM_RADIUS:.0f}, flora {PLANT_INNER:.0f}-{PLANT_OUTER:.0f}, hatch {HATCH}, "
      f"pilots at x {SPAWN_X:.0f}")
print(f"  halo: r {HALO_RADIUS:.1f} ({HALO_COUNT} rings {spacing:.0f} u apart, mouth {HALO_MOUTH:.0f}, {HALO_TO_BREAK} to break), "
      f"guard posts r {GUARD_POST_RADIUS:.1f}; the dance reaches {DANCE_REACH_NEEDED:.0f} of its {director['DanceReach']:.0f}")
print(f"  scene: {SCENE_SOURCE}")

g.finish(errors, referenced=EXISTING,
         minted=list(G_SCRIPT.values()) + list(G_ASSET.values()) + list(G_PLAN.values())
                + [guid("folder/" + d) for d in (MODE_DIR, SWARM_DIR, CELL_DIR)]
                + [guid("text/Assets/_Scripts/Controller/Arcade/TANDAVA.md")])
