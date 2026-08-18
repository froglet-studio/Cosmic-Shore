#!/usr/bin/env python3
"""
Author the LATTICE cell - the freestyle Cell-Selector world whose entire environment is a
living population of the two lattice flora species, one colony per element (8 colonies).

WHY A SCRIPT (the /ecology skill §7.1 rule): a cell whose mass comes from FLORA has to have
its phase ladder authored against the forest it will actually grow, and the two lattice
species' per-prism volumes span 130x (SchwarzP Charge 0.85 -> gyroid Mass 110.25). Eight
hand-typed thresholds drift the first time one leaf is refitted. This file holds the model,
reads every leaf size back off the SHIPPED per-element species assets so it can never
disagree with them, prints the table, and supports --check.

THE MODEL
---------
Species        one colony per (species, element). The element IDENTITY (leaf size, grow
               tempo, lattice scale, shield cadence, per-plant budget) is copied verbatim
               from _SO_Assets/Lifeforms/{Gyroid,SchwarzP} Flora {Element}.asset - those
               assets are the element palette, and forking their identity here would be two
               sources of truth for one plant's shape.

Population     floor / cap are the ONLY numbers this cell relaxes, and they are relaxed for
               a stated reason: the shipped caps (gyroid 33-60, SchwarzP 22) descend from
               "one big plant became many small ones of the SAME TOTAL MASS"
               (author_flora_populations.py). This cell is not a conversion of an authored
               plant - it is a cell whose environment IS the colony, so its ceiling is the
               collider budget and the volume ladder, not a legacy per-plant budget.

Prisms        gyroid: a plant OWNS a 24-prism octagon and is budgeted 30 for the boundary
              prisms its ownership epsilon lets it win (measured patches run 22-28).
              SchwarzP: a plant owns exactly one TILE - an integer partition of the surface -
              so patch == budget == 36 and there is no contested boundary.
              The PRISM TARGET is therefore quoted twice: at the patch (what plants settle
              at) and at the budget (the ceiling). The ladder is sized off the ceiling.

Ladder        derived from ONE set of ratios against the mature forest, so every threshold
              moves together when a population or a leaf changes. FrenzyEXIT sits ABOVE the
              mature forest on purpose: a Frenzy can then only ever be caused by vessel
              trail on top of a full garden, and it always releases back with the forest
              intact. If exit sat below mature, a cell that froze once would need active
              grazing before it could resume - a garden that punishes visitors.

NO SELF-TEST AGAINST A SHIPPED LADDER, and that is stated rather than hidden: this is the
first flora-only cell, so there is no play-tested ladder of this shape to reproduce (the
/ecology §7.1 self-test rule assumes one exists). What stands in for it is (a) reading the
leaf geometry off the shipped assets instead of restating it, and (b) the ORDERING asserts
in verify() - the same "assert the relationship, not the values" rule §34.8 leaves behind.

Usage:
    python3 Tools/Build/author_lattice_cell.py            # write + print the table
    python3 Tools/Build/author_lattice_cell.py --check    # verify, exit 1 on drift
"""

import hashlib
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SO = os.path.join(REPO, "Assets", "_SO_Assets")
CELL_DIR = os.path.join(SO, "Cell Configs", "Lattice Cell")
LIFEFORMS = os.path.join(SO, "Lifeforms")

CELL_NAME = "Lattice"
PREFIX = "Lattice"

# ── Script GUIDs (the SO types these assets are instances of) ────────────────
SCRIPT = {
    "cell":    "01f934d50526431a9392a6ceca1dc33d",   # CellConfigDataSO
    "profile": "e8d8aa5d835249798a256e18f2f7d912",   # SpawnProfileSO
    "flora":   "a32a297a7606432885f4d3e1f83bea9a",   # FloraConfigurationSO
    "fauna":   "c778cfbe4dfc4c5c8401e40c17802311",   # FaunaConfigurationSO
}

# ── Cell-owned visuals: the SAME prefabs the Blob cell uses ─────────────────
# Never a scene-placed or rescaled copy - a new core size means a new config pointing at a
# resized prefab (CLAUDE.md / Docs/ECOSYSTEM.md §13.1). This cell wants the standard core.
MEMBRANE = ("346633111830028674", "6e330f85972faf843b8a128e7166f7b5")
NUCLEUS = ("7555898194514117247", "b9cf1833fa2493d4b8724ccb6740fb3a")
CYTOPLASM = ("639495419069806261", "9cacd903fcf4643459f5f14ac811bb20")
MODIFIER = ("8058406376250941529", "daa37ae0e7af4b04383c1c4e6e76817d")
ICON = "6aa1c06e11b265744a5f9fa8858ac72a"          # shared with Blob - no new art authored
TADPOLE_PREFAB = ("5945480239701989318", "c7fd418d426de8740ac888dcc23a5d24")

MEMBRANE_RADIUS = 1200.0    # CapsuleMembrane.prefab -> radius
NUCLEUS_RADIUS = 392.0      # Node2 half-extent 0.9798 x Nucleus.prefab scale 400

# ── Population: the one thing this cell relaxes ─────────────────────────────
INITIAL_SPAWN = 8     # founders planted at t=0 (per species)
FLOOR = 30            # seed floor - the seeder tops back up to this every PlantPeriod (120s)
CAP = 90              # hard per-species cap; the real bound is the ladder + grazing

QUOTA = 22            # GrowthPerOffspring (inert for a colony birth, but must be sane)
COOLDOWN = 5
MATURITY = 0.5
SPREAD = 40

# ── The garden BAND (volume-uniform between these fractions of the membrane) ─
# Outer 0.75 keeps the colonies' outward growth well inside the sense radius; inner 0.38 is
# just outside the nucleus. The inner edge matters: the shipped per-element assets author
# PlantRadiusCellFraction 0.2 (240u), which is INSIDE the ~392u nucleus, so ResolvePlantRadius
# collapses to a single degenerate shell there. A cell with eight colonies needs the room.
BAND_OUTER = 0.75
BAND_INNER = 0.38

# ── Ecosystem heartbeat: also the colony BIRTH clock ────────────────────────
# A lattice colony births exactly ONE daughter per fauna-wave period, so this is the dial that
# decides how long the garden takes to fill: (CAP - FLOOR) x HEARTBEAT per species, in
# parallel across the eight. 10s -> ~10 minutes. Lower it and the fauna waves quicken too.
HEARTBEAT = 10.0

# ── Ladder ratios (against the mature forest at the per-plant BUDGET ceiling) ─
FRENZY_ENTER_RATIO = 1.30
FRENZY_EXIT_RATIO = 1.10
RESTLESS_ENTER_RATIO = 0.22
RESTLESS_EXIT_RATIO = 0.16

# species key -> (source asset in Lifeforms, patch, budget)
SPECIES = []
for element in ("Charge", "Mass", "Space", "Time"):
    SPECIES.append(("Gyroid", element, 24, 30))
for element in ("Charge", "Mass", "Space", "Time"):
    SPECIES.append(("SchwarzP", element, 36, 36))

ELEMENT_ID = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}


def read(path):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        return fh.read()


def guid_for(name):
    """Deterministic asset GUID, so re-running never re-mints references."""
    return hashlib.md5(f"cosmicshore/lattice-cell/{name}".encode()).hexdigest()


def source_asset(family, element):
    return os.path.join(LIFEFORMS, f"{family} Flora {element}.asset")


def variant_block(family, element):
    """The element's IDENTITY, read verbatim off the shipped per-element species asset."""
    text = read(source_asset(family, element))
    match = re.search(r"^  Variant:\n((?:    .*\n)+)", text, flags=re.M)
    if not match:
        raise SystemExit(f"{family} {element}: no Variant block in the source asset")
    lines = [ln for ln in match.group(1).splitlines()
             # This cell authors its own planting band - drop the source's.
             if not ln.strip().startswith(("PlantRadiusCellFraction:",
                                           "PlantRadiusCellFractionMin:"))]
    lines.append(f"    PlantRadiusCellFraction: {BAND_OUTER}")
    lines.append(f"    PlantRadiusCellFractionMin: {BAND_INNER}")
    return "\n".join(lines) + "\n"


def leaf_volume(family, element):
    text = read(source_asset(family, element))
    m = re.search(r"LeafSize: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+)\}", text)
    if not m:
        raise SystemExit(f"{family} {element}: no LeafSize in the source asset")
    x, y, z = (float(v) for v in m.groups())
    return x * y * z


def prefab_ref(family):
    """The flora prefab this family's species assets point at."""
    text = read(source_asset(family, "Charge"))
    m = re.search(r"FloraPrefab: \{fileID: (-?\d+), guid: ([a-f0-9]{32})", text)
    return m.group(1), m.group(2)


# ── The model ───────────────────────────────────────────────────────────────

def budget():
    rows = []
    for family, element, patch, plant_budget in SPECIES:
        vol = leaf_volume(family, element)
        rows.append(dict(
            family=family, element=element, patch=patch, budget=plant_budget,
            leaf_volume=vol,
            prisms_settled=CAP * patch,
            prisms_ceiling=CAP * plant_budget,
            volume_ceiling=CAP * plant_budget * vol,
            prisms_seeded=FLOOR * patch,
        ))
    return rows


def totals(rows):
    return dict(
        settled=sum(r["prisms_settled"] for r in rows),
        ceiling=sum(r["prisms_ceiling"] for r in rows),
        volume=sum(r["volume_ceiling"] for r in rows),
        seeded=sum(r["prisms_seeded"] for r in rows),
        plants=CAP * len(rows),
        plants_seeded=FLOOR * len(rows),
    )


def round_to(value, step):
    return int(round(value / step) * step)


def ladder(rows):
    t = totals(rows)
    v, n = t["volume"], t["ceiling"]
    return dict(
        RestlessEnter=round_to(n * RESTLESS_ENTER_RATIO, 100),
        RestlessExit=round_to(n * RESTLESS_EXIT_RATIO, 100),
        FrenzyEnter=round_to(n * FRENZY_ENTER_RATIO, 100),
        FrenzyExit=round_to(n * FRENZY_EXIT_RATIO, 100),
        RestlessEnterVolume=round_to(v * RESTLESS_ENTER_RATIO, 1000),
        RestlessExitVolume=round_to(v * RESTLESS_EXIT_RATIO, 1000),
        FrenzyEnterVolume=round_to(v * FRENZY_ENTER_RATIO, 1000),
        FrenzyExitVolume=round_to(v * FRENZY_EXIT_RATIO, 1000),
    )


def verify(rows):
    """Assert the RELATIONSHIPS, not the numbers (§34.8's rule)."""
    t, L = totals(rows), ladder(rows)
    problems = []

    if t["settled"] <= 20000:
        problems.append(f"settled prism target missed: {t['settled']} <= 20000")

    # The forest must fit UNDER both ceilings, or the cell freezes while still growing.
    if L["FrenzyEnterVolume"] <= t["volume"]:
        problems.append("FrenzyEnterVolume is below the mature forest - the garden freezes")
    if L["FrenzyEnter"] <= t["ceiling"]:
        problems.append("FrenzyEnter (count backstop) is below the mature forest")

    # A Frenzy caused by trail must always release with the forest intact.
    if L["FrenzyExitVolume"] <= t["volume"]:
        problems.append("FrenzyExitVolume is below the mature forest - a frozen cell "
                        "would need active grazing before it could resume growing")

    # Fauna must start hunting a PARTLY grown cell, not a finished one.
    seeded_volume = t["volume"] * FLOOR / CAP
    if L["RestlessEnterVolume"] >= t["volume"]:
        problems.append("RestlessEnterVolume is at/above the mature forest - fauna never hunt")
    if L["RestlessEnterVolume"] > seeded_volume:
        problems.append("RestlessEnterVolume is above the SEEDED forest - the food web is "
                        "asleep through the whole bootstrap")

    # Hysteresis must be a band, in the right direction.
    for hi, lo in (("FrenzyEnterVolume", "FrenzyExitVolume"),
                   ("RestlessEnterVolume", "RestlessExitVolume"),
                   ("FrenzyEnter", "FrenzyExit"), ("RestlessEnter", "RestlessExit")):
        if L[hi] <= L[lo]:
            problems.append(f"{hi} must exceed {lo}")

    # The planting band must clear the nucleus: that mass is the territorial claim and is
    # excluded from the fauna targeting grids.
    if MEMBRANE_RADIUS * BAND_INNER <= NUCLEUS_RADIUS:
        problems.append("planting band's inner edge is inside the nucleus")
    if BAND_OUTER <= BAND_INNER:
        problems.append("planting band is inverted")

    return problems


# ── Asset emission ──────────────────────────────────────────────────────────

HEADER = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: {name}
  m_EditorClassIdentifier: 
"""

META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def flora_asset(family, element):
    name = f"{PREFIX} {family} {element} Flora Config Data"
    file_id, prefab_guid = prefab_ref(family)
    body = HEADER.format(script=SCRIPT["flora"], name=name)
    body += f"  FloraPrefab: {{fileID: {file_id}, guid: {prefab_guid}, type: 3}}\n"
    body += "  SpawnProbability: 1\n"
    body += f"  InitialSpawnCount: {INITIAL_SPAWN}\n"
    body += "  OverrideDefaultPlantPeriod: 0\n"
    body += "  NewPlantPeriod: 9999999\n"
    body += f"  PopulationSize: {FLOOR}\n"
    body += f"  MaxLivePopulation: {CAP}\n"
    body += f"  GrowthPerOffspring: {QUOTA}\n"
    body += "  OffspringPerBirth: 1\n"
    body += f"  ReproductionCooldownSeconds: {COOLDOWN}\n"
    body += f"  MaturityFraction: {MATURITY}\n"
    body += f"  OffspringSpread: {SPREAD}\n"
    body += f"  Element: {ELEMENT_ID[element]}\n"
    body += "  Variant:\n" + variant_block(family, element)
    body += "  InitialLevel: 1\n"
    body += "  LeafScalePerLevel: 1.15\n"
    return name, body


def fauna_asset():
    name = f"{PREFIX} Tadpole Fauna Config Data"
    file_id, prefab_guid = TADPOLE_PREFAB
    body = HEADER.format(script=SCRIPT["fauna"], name=name)
    body += f"  FaunaPrefab: {{fileID: {file_id}, guid: {prefab_guid}, type: 3}}\n"
    body += """  InitialSpawnCount: 4
  PopulationSize: 4
  SpawnProbability: 1
  FeedsPerOffspring: 20
  FeedsPerLevel: 40
  OffspringPerBirth: 1
  ReproductionCooldownSeconds: 10
  MaxLivePopulation: 8
  CenterFocusBias: 0.35
  Element: 2
  InitialLevel: 1
  BodyScalePerLevel: 1.15
  LevelGrowSeconds: 1
  Variant:
    Enabled: 1
    BaseBodyScale: 0.4
    BodyPrismScale: {x: 0.8, y: 0.8, z: 7}
    StarvationSeconds: 90
    Forager: 1
    CohesionRadius: 50
    BehaviorUpdateRate: 1.5
    TrailBlockInteractionRadius: 20
    GoalWeight: 3
    MinSpeed: 10
    MaxSpeed: 15
    OverrideAudio: 0
    AudioLoopEvent:
      Guid:
        Data1: 0
        Data2: 0
        Data3: 0
        Data4: 0
      Path: 
    AudioMinDistance: -1
    AudioMaxDistance: -1
"""
    return name, body


def profile_asset(flora_names, fauna_name):
    name = f"{CELL_NAME} Cell Spawn Profile"
    body = HEADER.format(script=SCRIPT["profile"], name=name)
    body += """  FloraExcludeLocalDomain: 1
  FloraSpawnVolumeCeiling: 12000
  FloraInitialDelaySeconds: 0
  FloraSpawnIntervalSeconds: 0
  FloraPopulationScale: 1
  FloraPlantBudgetScale: 1
  SupportedFloras:
"""
    for n in flora_names:
        body += f"  - {{fileID: 11400000, guid: {guid_for(n)}, type: 2}}\n"
    body += f"""  FaunaExcludeLocalDomain: 1
  InitialFaunaSpawnWaitTime: 20
  FaunaSpawnVolumeThreshold: 1
  FaunaPopulationScale: 1
  BaseFaunaSpawnTime: {HEARTBEAT:g}
  SeedFullWaveEveryTick: 0
  FaunaFoodFloor: 5
  FaunaInitialDelaySeconds: 0
  FaunaSpawnIntervalSeconds: 0
  HerbivoreSpawnPointCount: 3
  HerbivoreSpawnRadius: 700
  PredatorSpawnPointCount: 0
  PredatorSpawnRadius: 900
  SupportedFaunas:
  - {{fileID: 11400000, guid: {guid_for(fauna_name)}, type: 2}}
"""
    return name, body


def cell_asset(profile_name, L):
    name = f"{CELL_NAME} Cell Config"
    body = HEADER.format(script=SCRIPT["cell"], name=name)
    body += f"""  CellName: {CELL_NAME}
  Description: Eight lattice colonies - gyroid and Schwarz P, one of each element - growing
    into one another
  Icon: {{fileID: 21300000, guid: {ICON}, type: 3}}
  Difficulty: 2
  CellEndGameScore: 0
  MembranePrefab: {{fileID: {MEMBRANE[0]}, guid: {MEMBRANE[1]}, type: 3}}
  NucleusPrefab: {{fileID: {NUCLEUS[0]}, guid: {NUCLEUS[1]}, type: 3}}
  CytoplasmPrefab: {{fileID: {CYTOPLASM[0]}, guid: {CYTOPLASM[1]}, type: 3}}
  CellModifiers:
  - {{fileID: {MODIFIER[0]}, guid: {MODIFIER[1]}, type: 3}}
  SpawnProfile: {{fileID: 11400000, guid: {guid_for(profile_name)}, type: 2}}
  PhaseThresholds:
    RestlessEnter: {L['RestlessEnter']}
    RestlessExit: {L['RestlessExit']}
    FrenzyEnter: {L['FrenzyEnter']}
    FrenzyExit: {L['FrenzyExit']}
    RestlessEnterVolume: {L['RestlessEnterVolume']}
    RestlessExitVolume: {L['RestlessExitVolume']}
    FrenzyEnterVolume: {L['FrenzyEnterVolume']}
    FrenzyExitVolume: {L['FrenzyExitVolume']}
"""
    return name, body


def emit(check):
    rows = budget()
    L = ladder(rows)

    assets = []
    flora_names = []
    for family, element, _, _ in SPECIES:
        n, b = flora_asset(family, element)
        flora_names.append(n)
        assets.append((n, b))
    fn, fb = fauna_asset()
    assets.append((fn, fb))
    pn, pb = profile_asset(flora_names, fn)
    assets.append((pn, pb))
    cn, cb = cell_asset(pn, L)
    assets.append((cn, cb))

    drift = []
    if not check:
        os.makedirs(CELL_DIR, exist_ok=True)
    for name, body in assets:
        path = os.path.join(CELL_DIR, name + ".asset")
        meta = META.format(guid=guid_for(name))
        for target, content in ((path, body), (path + ".meta", meta)):
            existing = read(target) if os.path.exists(target) else None
            if existing == content:
                continue
            if check:
                drift.append(os.path.relpath(target, REPO))
            else:
                with open(target, "w", encoding="utf-8") as fh:
                    fh.write(content)
    return rows, L, drift, [n for n, _ in assets]


def main():
    check = "--check" in sys.argv
    rows, L, drift, names = emit(check)
    t = totals(rows)

    print(f"{'species':<20}{'plants':>7}{'leaf vol':>10}{'settled':>9}{'ceiling':>9}{'volume':>12}")
    for r in rows:
        print(f"{r['family'] + ' ' + r['element']:<20}{CAP:>7}{r['leaf_volume']:>10.2f}"
              f"{r['prisms_settled']:>9}{r['prisms_ceiling']:>9}{r['volume_ceiling']:>12,.0f}")
    print(f"{'TOTAL':<20}{t['plants']:>7}{'':>10}{t['settled']:>9}{t['ceiling']:>9}"
          f"{t['volume']:>12,.0f}")
    print()
    print(f"  seeded (floor {FLOOR}/species): {t['plants_seeded']} plants, "
          f"{t['seeded']:,} prisms, {t['volume'] * FLOOR / CAP:,.0f} volume")
    print(f"  fill time: ({CAP} - {FLOOR}) x {HEARTBEAT:g}s heartbeat "
          f"= ~{(CAP - FLOOR) * HEARTBEAT / 60:.0f} min (colonies fill in parallel)")
    print(f"  heart-crystal colliders at cap: {t['plants']} (one per plant, always on)")
    print()
    print("  ladder: " + "  ".join(f"{k}={v:g}" for k, v in L.items()))
    print()

    problems = verify(rows)
    for p in problems:
        print(f"  FAIL: {p}")
    if problems:
        return 1

    if check:
        if drift:
            print("DRIFT - these assets no longer match the model:")
            for d in drift:
                print("  " + d)
            return 1
        print(f"OK - {len(names)} assets match the model.")
    else:
        print(f"Wrote {len(names)} assets to "
              f"{os.path.relpath(CELL_DIR, REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
