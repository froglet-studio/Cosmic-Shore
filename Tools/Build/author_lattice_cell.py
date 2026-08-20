#!/usr/bin/env python3
"""
Author the LATTICE cell - the freestyle Cell-Selector world whose entire environment is a
living population of the three lattice flora species, one colony per element (12 colonies).

WHY A SCRIPT (the /ecology skill §7.1 rule): a cell whose mass comes from FLORA has to have
its phase ladder authored against the forest it will actually grow, and the three lattice
species' per-prism volumes span 159x (SchwarzP Charge 0.85 -> quasicrystal Mass 135). Eight
hand-typed thresholds drift the first time one leaf is refitted. This file holds the model,
reads every leaf size back off the SHIPPED per-element species assets so it can never
disagree with them, prints the table, and supports --check.

THE MODEL
---------
Species        one colony per (species, element). The element IDENTITY (leaf size, grow
               tempo, lattice scale, shield cadence, per-plant budget) is copied verbatim
               from _SO_Assets/Lifeforms/{Gyroid,SchwarzP,Quasicrystal} Flora
               {Element}.asset - those
               assets are the element palette, and forking their identity here would be two
               sources of truth for one plant's shape.

Population     floor / cap are the ONLY numbers this cell relaxes, and they are relaxed for
               a stated reason: the shipped caps (gyroid 33-60, SchwarzP 22, quasicrystal 14)
               descend from
               "one big plant became many small ones of the SAME TOTAL MASS"
               (author_flora_populations.py). This cell is not a conversion of an authored
               plant - it is a cell whose environment IS the colony, so its ceiling is the
               collider budget and the volume ladder, not a legacy per-plant budget.

Prisms        gyroid: a plant OWNS a 24-prism octagon and is budgeted 30 for the boundary
              prisms its ownership epsilon lets it win (measured patches run 22-28).
              SchwarzP: a plant owns exactly one TILE - an integer partition of the surface -
              so patch == budget == 36 and there is no contested boundary.
              Quasicrystal: a plant owns one HEART's TREE cell on the aperiodic lattice - an
              exact integer partition, but one whose size legitimately varies (44..97 struts,
              mean 58.8 measured), so patch is the MEAN and budget sits above the measured max.
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

# ── Population: ONE founder per colony, and a high ceiling ──────────────────
# EXACTLY ONE SEED PER COLONY - twelve in the whole cell - and this is the cell's defining
# choice, not a tuning
# value. A lattice colony is ONE continuous minimal surface grown outward from its founder
# (Docs/ECOSYSTEM.md 32.7: 273 plants from one founder, a single connected gyroid), so every
# extra founder is an extra INDEPENDENT lattice frame - and independent frames cannot mate.
# AssembledFlora declines any site within MisalignmentRadius of a foreign frame, so N founders
# per species do not build one big structure N times faster: they build N small ones that stop
# against each other, which reads as a scattered forest (the Rampage look) instead of a
# superstructure. Seeding one and letting it reproduce is the whole mechanic.
#
# The seeder's job here is therefore EXTINCTION RECOVERY only: floor 1 means it replants a
# colony the food web wiped out, and does nothing at all while any plant of that species lives.
# (author_flora_populations.py floors lattice species at 4 founders for a reason that does not
# apply here - it protects the ELEMENT SPREAD of a config that rolls its element per plant.
# These eight configs each author ONE fixed element, so there is no spread to protect.)
INITIAL_SPAWN = 1     # founders planted at t=0, per species -> 12 in the cell
FLOOR = 1             # seed floor = extinction recovery only
CAP = 90              # how large ONE superstructure may get; the real bound is the ladder + grazing

QUOTA = 22            # GrowthPerOffspring (inert for a colony birth, but must be sane)
COOLDOWN = 5
MATURITY = 0.5
SPREAD = 40

# ── The garden BAND (volume-uniform between these fractions of the membrane) ─
# This band places the TWELVE FOUNDERS and nothing else - every other plant is placed by its
# parent's own lattice frontier, not by a radius roll. So it is not "where the forest goes", it
# is "where the twelve seeds go", and it is a MID-SHELL (540u - 840u of the 1200u membrane) so
# each superstructure has room to grow both inward and outward before it meets anything.
# Twelve points on a ~700u shell sit ~600u apart - still wider than the largest superstructure
# (the Space quasicrystal, whose LatticeScale 2 puts 90 hearts inside a ~300u ball). Two frames
# that DO meet simply stop against one another; nothing overlaps, because every strut still
# passes PrismSpatialIndex.TryReserve.
#
# The inner edge still matters for the reason it always did: the shipped per-element assets
# author PlantRadiusCellFraction 0.2 (240u), which is INSIDE the ~392u nucleus, so
# ResolvePlantRadius collapses to a single degenerate shell there - every founder on one sphere,
# inside the territorial claim.
BAND_OUTER = 0.70
BAND_INNER = 0.45

# ── Ecosystem heartbeat: also the colony BIRTH clock ────────────────────────
# A lattice colony births exactly ONE daughter per fauna-wave period - REGARDLESS of how many
# plants it already has - so this is the dial that decides how long a superstructure takes to
# build itself: (CAP - 1) x HEARTBEAT per colony, in parallel across the eight. Growth is
# linear in the cap, which is why one founder needs a quicker heartbeat than a seeded forest
# did. Lower it and the fauna waves quicken too; they share the clock by design.
HEARTBEAT = 5.0

# ── Ladder ratios (against the mature forest at the per-plant BUDGET ceiling) ─
FRENZY_ENTER_RATIO = 1.30
FRENZY_EXIT_RATIO = 1.10
RESTLESS_ENTER_RATIO = 0.22
RESTLESS_EXIT_RATIO = 0.16

# species key -> (source asset in Lifeforms, patch, budget)
# Patch/budget must agree with author_flora_populations.py's LATTICE_PATCH - the same two
# numbers describe the same plant there, and a fork would give one species two sizes.
SPECIES = []
for element in ("Charge", "Mass", "Space", "Time"):
    SPECIES.append(("Gyroid", element, 24, 30))
for element in ("Charge", "Mass", "Space", "Time"):
    SPECIES.append(("SchwarzP", element, 36, 36))
for element in ("Charge", "Mass", "Space", "Time"):
    SPECIES.append(("Quasicrystal", element, 59, 110))

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

    # Every colony must be able to REACH its cap before the cell freezes. The ladder is
    # derived from the whole forest, so one very heavy species can in principle put Frenzy
    # below a lighter colony's own ceiling only if the ratios invert - assert it directly.
    for r in rows:
        if r["volume_ceiling"] >= L["FrenzyEnterVolume"]:
            problems.append(f"{r['family']} {r['element']} alone reaches FrenzyEnterVolume")

    # The forest must fit UNDER both ceilings, or the cell freezes while still growing.
    if L["FrenzyEnterVolume"] <= t["volume"]:
        problems.append("FrenzyEnterVolume is below the mature forest - the garden freezes")
    if L["FrenzyEnter"] <= t["ceiling"]:
        problems.append("FrenzyEnter (count backstop) is below the mature forest")

    # A Frenzy caused by trail must always release with the forest intact.
    if L["FrenzyExitVolume"] <= t["volume"]:
        problems.append("FrenzyExitVolume is below the mature forest - a frozen cell "
                        "would need active grazing before it could resume growing")

    # Fauna must start hunting a PARTLY grown cell, not a finished one. The cell opens with
    # twelve lone founders, so there is no seeded forest to compare against - what matters is
    # that Restless lands a good way BELOW mature, i.e. while the colonies are still building.
    if L["RestlessEnterVolume"] >= t["volume"] * 0.5:
        problems.append("RestlessEnterVolume is too near the mature forest - fauna would only "
                        "wake once the superstructures are already finished")
    if L["RestlessEnterVolume"] <= 0:
        problems.append("RestlessEnterVolume must be positive")

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
  Description: Twelve lattice colonies - gyroid, Schwarz P and icosahedral quasicrystal, one
    of each element - growing into one another
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
    print(f"  founders: {t['plants_seeded']} ({INITIAL_SPAWN}/species) - one lattice frame per "
          f"colony, floor {FLOOR} = extinction recovery only")
    print(f"  build time: ({CAP} - 1) x {HEARTBEAT:g}s heartbeat "
          f"= ~{(CAP - 1) * HEARTBEAT / 60:.0f} min per superstructure "
          f"(all {len(rows)} in parallel)")
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
