#!/usr/bin/env python3
"""
Author the ARBORETUM cell - the freestyle Cell-Selector world that is a COLLECTION OF
SPECIMENS: one plant of each of the four Mandelbulb species AND of the Borromean membrane,
in each of the four elements - TWENTY in all, and nothing else.

WHY A SCRIPT (the /ecology skill's rule, and the /flora skill §6): a cell whose mass comes
entirely from FLORA has to have its phase ladder authored against the forest it will actually
grow, and these twenty plants' per-prism volumes span two orders of magnitude (FractalFoliage
Charge 0.61 median -> Borromean Mass 72.87). Eight hand-typed thresholds drift the first time
one leaf is refitted - which just happened (Docs/ECOSYSTEM.md §56 moved every Mass and Space
cross-section in the Mandelbulb family). This file holds the model, GROWS each of the sixteen
Mandelbulb plants through the shipped growth rule to measure it, READS the four Borromean
specimens out of their own measured table, prints the table, and supports --check.

THE MODEL
---------
Roster      one plant per (species, element), over FIVE species: the four Mandelbulb ones and
            the Borromean membrane. The element IDENTITY - leaf, heart, per-plant budget,
            grow tempo - is never authored here. For the Mandelbulb four it is copied
            VERBATIM off _SO_Assets/Lifeforms/<species> Flora <Element>.asset, because those
            sixteen assets are the element palette and forking their identity here would be
            two sources of truth for one plant's shape. The Borromean four are not authored
            here AT ALL: author_borromean_flora_assets.py owns that species' configs in
            every cell that grows it (its DEPLOYMENTS table), so this script READS the four
            it wrote - their GUIDs off their own .meta, their measured budget and plate out
            of BorromeanSurfaceData.cs through that tool's own reader - and fails by name if
            they are missing. This cell authors only the population (floor, cap, initial),
            the planting band, the SupportedFloras list, and the ladder.

Population  FLOOR 1, CAP 1, INITIAL 1. "One of each" is the cell, not a tuning value:
            it is an ARBORETUM, a collection of specimens, so each config holds exactly one.
            That is a cap, never a cull - the plant keeps its authored growth quota and simply
            cannot spend it while it is the only one of its kind alive, and the seeder's whole
            remaining job is EXTINCTION RECOVERY (a specimen the food web strips to nothing is
            replanted). No timer, no decay, no imposed death: CLAUDE.md's conserved-mass law
            in full.

Prisms      per-plant budgets are GEOMETRY on both families and are quoted, never
            re-authored: the Mandelbulb budget is 4,150 because §55 sized it as "the budget
            at which the same amount of CURVE is laid as before the plants grew limbs",
            Apollonia's 2,900 because a gasket's form is finite and priced by DiscMinRadius
            rather than by a budget, and a Borromean plant's is its element's whole site
            table (180..360) because that surface is COMPACT - it closes on itself and is
            finished (§49). Cutting any of them would ship a truncated specimen, which is the
            one thing an arboretum may not do.

Ladder      derived from ONE set of ratios against the mature garden, so every threshold moves
            together when a leaf changes. FrenzyEXIT sits ABOVE the mature garden on purpose
            (Docs/ECOSYSTEM.md §36): the sixteen are hard-capped, so a Frenzy here can only
            ever be caused by vessel TRAIL on top of a full garden, and it must always release
            with the garden intact.

WHAT IT DELIBERATELY IS NOT: a forest. There is no EnvironmentPrefab and no second producer -
the cell IS its twenty specimens, the way the Lattice cell IS its twelve colonies. It is also
not the Spawn Matrix bench, which lines the same species up in a row for comparison; this
is a WORLD you fly through and meet them in.

WHY THE BORROMEAN FOUR BELONG HERE: the species is the one in the project whose four elements
are each FITTED rather than typed - Time the anchor, Mass the chunkiest plate, Space the same
volume at 8.5:1 on twice the membrane, Charge a square slab fitted to its own shielded
octahedra - so a 19.6x spread in plant volume across one species is the clearest statement
the fleet has of what an element IS. That is the same sentence §56 spent on the Mandelbulb
reach, said by a compact surface instead of a fractal cage, which is exactly the comparison
this cell exists to make.

Usage:
    python3 Tools/Build/author_arboretum_cell.py            # write + print the table
    python3 Tools/Build/author_arboretum_cell.py --check    # verify, exit 1 on drift
"""

from __future__ import annotations

import argparse
import hashlib
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import author_borromean_flora_assets as borromean  # noqa: E402
import mandelbulb_flora_model as M  # noqa: E402
import measure_mandelbulb_flora as measure  # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SO = os.path.join(REPO, "Assets", "_SO_Assets")
CELL_DIR = os.path.join(SO, "Cell Configs", "Arboretum Cell")
LIFEFORMS = os.path.join(SO, "Lifeforms")
MENU_SCENE = os.path.join(REPO, "Assets", "_Scenes", "Menu_Main.unity")

CELL_NAME = "Arboretum"
PREFIX = "Arboretum"

# ── Script GUIDs (the SO types these assets are instances of) ────────────────
SCRIPT = {
    "cell":    "01f934d50526431a9392a6ceca1dc33d",   # CellConfigDataSO
    "profile": "e8d8aa5d835249798a256e18f2f7d912",   # SpawnProfileSO
    "flora":   "a32a297a7606432885f4d3e1f83bea9a",   # FloraConfigurationSO
    "fauna":   "c778cfbe4dfc4c5c8401e40c17802311",   # FaunaConfigurationSO
}

# ── Cell-owned visuals: the SAME prefabs every freestyle cell uses ──────────
# Never a scene-placed or rescaled copy - a new core size means a new config pointing at a
# resized prefab (CLAUDE.md / Docs/ECOSYSTEM.md §13.1). This cell wants the standard core.
MEMBRANE = ("346633111830028674", "6e330f85972faf843b8a128e7166f7b5")
NUCLEUS = ("7555898194514117247", "b9cf1833fa2493d4b8724ccb6740fb3a")
CYTOPLASM = ("639495419069806261", "9cacd903fcf4643459f5f14ac811bb20")
MODIFIER = ("8058406376250941529", "daa37ae0e7af4b04383c1c4e6e76817d")
ICON = "6aa1c06e11b265744a5f9fa8858ac72a"          # shared - no new art authored
TADPOLE_PREFAB = ("5945480239701989318", "c7fd418d426de8740ac888dcc23a5d24")

MEMBRANE_RADIUS = 1200.0    # CapsuleMembrane.prefab -> radius
NUCLEUS_RADIUS = 392.0      # Node2 half-extent 0.9798 x Nucleus.prefab scale 400

# ── Population ──────────────────────────────────────────────────────────────
INITIAL_SPAWN = 1     # specimens planted at t=0, per config -> 16 in the cell
FLOOR = 1             # seed floor = extinction recovery only
CAP = 1               # ONE of each. This is the cell.

COOLDOWN = 5
MATURITY = 0.5
SPREAD = 200          # where a (refused) offspring would be placed; kept wide so a
                      # re-seed after a death does not land on the parent's own grave

# ── The garden BAND (volume-uniform between these fractions of the membrane) ─
# Twenty specimens, MEASURED at 108-294 units across (the table this script prints - never
# eyeballed, and note the §56 reach put a 1.72x SPAN between a Mass specimen and a Space one),
# placed in a WIDE shell so they read as a scattered arboretum rather than a ring. The twenty
# bounding spheres together occupy a few percent of the band, asserted in verify(), so a collision is
# unlikely and harmless when it happens: every prism still goes through
# PrismSpatialIndex.TryReserve, so two specimens that do meet simply stop against each other.
#
# The INNER edge is 504u, outside the ~392u nucleus, which matters for the reason it always
# does: Flora.ResolvePlantRadius clamps the band outside a control zone, so a band authored
# inside one collapses to a single degenerate shell with every specimen on one sphere.
BAND_OUTER = 0.92
BAND_INNER = 0.42

# ── Ecosystem heartbeat ─────────────────────────────────────────────────────
# The platform default. Unlike the Lattice cell this clock does NOT gate plant reproduction -
# the Mandelbulb family breeds on the per-plant growth quota, not on a colony cycle - so it is
# only the fauna wave period here (Docs/ECOSYSTEM.md §13).
HEARTBEAT = 30.0

# ── Ladder ratios (against the mature garden) ───────────────────────────────
# RESTLESS EARLY (~35% of the planting budget, or the food web is dormant for the whole of the
# cell's growth), FRENZY above the MATURE cell with trail headroom, and FRENZY EXIT above
# mature so a trail-caused freeze always releases with the garden intact (§36, §48).
RESTLESS_ENTER_RATIO = 0.35
RESTLESS_EXIT_RATIO = 0.26
FRENZY_ENTER_RATIO = 1.45
FRENZY_EXIT_RATIO = 1.25

# The reference cells this cell's collider budget is gated against - shipped numbers, never
# invented ones (the /flora skill §6 rule).
ATLANTIS_PRISMS = 69000           # the heaviest authored environment in the game
LATTICE_FRENZY_ENTER = 82400      # the closest sibling: a Cell-Selector cell that IS its plants
LATTICE_HEART_COLLIDERS = 1080

ELEMENT_ID = {"Charge": 1, "Mass": 2, "Space": 3, "Time": 4}
ELEMENTS = ("Charge", "Mass", "Space", "Time")


def read(path):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        return fh.read()


def guid_for(name):
    """Deterministic asset GUID, so re-running never re-mints references."""
    return hashlib.md5(f"cosmicshore/arboretum-cell/{name}".encode()).hexdigest()


def display(species):
    return M.SPECIES[species]["display"]


def source_asset(species, element):
    return os.path.join(LIFEFORMS, f"{display(species)} {element}.asset")


def variant_block(species, element):
    """The element's IDENTITY, read verbatim off the shipped per-element species asset.

    Everything in it - the heart size (owned by author_lifeform_heart_sizes.py), the per-plant
    budget, the grow period - belongs to another tool. Quoting it is the only way this cell can
    never become a second owner of a field; the ONE thing it overrides is the planting band,
    which is a property of this cell and of nothing else.
    """
    text = read(source_asset(species, element))
    match = re.search(r"^  Variant:\n((?:    .*\n)+)", text, flags=re.M)
    if not match:
        raise SystemExit(f"{species} {element}: no Variant block in the source asset")
    lines = [ln for ln in match.group(1).splitlines()
             if not ln.strip().startswith(("PlantRadiusCellFraction:",
                                           "PlantRadiusCellFractionMin:"))]
    lines.append(f"    PlantRadiusCellFraction: {BAND_OUTER}")
    lines.append(f"    PlantRadiusCellFractionMin: {BAND_INNER}")
    return "\n".join(lines) + "\n"


def source_field(species, element, field, pattern=r"([-\d.eE+]+)"):
    text = read(source_asset(species, element))
    m = re.search(rf"^  {field}: {pattern}$", text, flags=re.M)
    if not m:
        raise SystemExit(f"{species} {element}: no {field} in the source asset")
    return m.group(1)


def prefab_ref(species):
    """The flora prefab this species' assets point at.

    Read back rather than typed: the component fileID differs per growth family, and a wrong
    one resolves to no component at all and grows nothing, SILENTLY (the /flora skill §8).
    """
    text = read(source_asset(species, "Charge"))
    m = re.search(r"FloraPrefab: \{fileID: (-?\d+), guid: ([a-f0-9]{32})", text)
    if not m:
        raise SystemExit(f"{species}: no FloraPrefab in the source asset")
    return m.group(1), m.group(2)


# ── The model ───────────────────────────────────────────────────────────────

def borromean_config_name(element):
    """The asset author_borromean_flora_assets.py deploys into this cell's folder.

    The prefix is that tool's DEPLOYMENTS entry for this cell, not a name invented here -
    if the two ever disagree, borromean_state() reports the miss BY NAME rather than this
    cell silently growing nineteen plants.
    """
    return f"{PREFIX} Borromean Flora {element} Config Data"


def borromean_rows():
    """The four Borromean specimens, READ rather than authored.

    author_borromean_flora_assets.py owns this species' configs in every cell that grows it,
    so this consumes that tool's own table reader and its own GUID rule instead of keeping a
    second copy of either. A plant's prism count IS its element's site table, because a
    Borromean surface is COMPACT - it closes on itself and is finished, so there is no budget
    to stop it short (Docs/ECOSYSTEM.md §49); every plate in one plant is identical, so the
    per-prism spread is a point; and the plant RADIUS is the table's own.
    """
    tbl = borromean.read_table()
    rows = []
    for element in ELEMENTS:
        el = tbl["elements"][element]
        per = el["leaf"][0] * el["leaf"][1] * el["leaf"][2]
        name = borromean_config_name(element)
        rows.append(dict(
            family="Borromean", species="Borromean", display="Borromean",
            element=element, prisms=el["sites"], curves=0,
            volume=per * el["sites"], radius=el["radius"], dims=(per, per),
            asset=name, guid=borromean.guid(name + ".asset"), owned_here=False,
        ))
    return rows


def borromean_state(rows):
    """Are the four this cell depends on actually on disk, under the GUIDs we referenced?

    A SupportedFloras entry pointing at a GUID nothing owns resolves to no config at all and
    grows nothing, silently - the /flora skill §8 failure mode one level up. So the handoff
    is checked rather than assumed, and it names the tool that closes it.
    """
    missing = []
    for r in rows:
        if r["owned_here"]:
            continue
        meta = os.path.join(CELL_DIR, r["asset"] + ".asset.meta")
        if not os.path.exists(meta):
            missing.append(f"{r['asset']}.asset is missing - run "
                           f"Tools/Build/author_borromean_flora_assets.py --write")
        elif f"guid: {r['guid']}" not in read(meta):
            missing.append(f"{r['asset']}.asset.meta does not carry guid {r['guid']} - "
                           f"author_borromean_flora_assets.py's deployment prefix and this "
                           f"cell's PREFIX disagree")
    return missing


def budget():
    """GROW the sixteen Mandelbulb specimens through the shipped rule, READ the Borromean four."""
    rows = []
    for species in M.SPECIES:
        for element in ELEMENTS:
            r = measure.element_report(element, species=species)
            name = flora_asset_name(species, element)
            rows.append(dict(
                family="Mandelbulb", species=species, display=display(species),
                element=element,
                prisms=r["prisms"], curves=r["curves"],
                volume=r["volume"], radius=r["radius"],
                dims=r["dims"],
                quota=source_field(species, element, "GrowthPerOffspring"),
                asset=name, guid=guid_for(name), owned_here=True,
            ))
    return rows + borromean_rows()


def totals(rows):
    return dict(
        prisms=sum(r["prisms"] for r in rows) * CAP,
        volume=sum(r["volume"] for r in rows) * CAP,
        plants=CAP * len(rows),
        seeded=FLOOR * len(rows),
        heaviest=max(r["volume"] for r in rows) * CAP,
        widest=max(r["radius"] for r in rows),
    )


def round_to(value, step):
    return int(round(value / step) * step)


def ladder(rows):
    t = totals(rows)
    v, n = t["volume"], t["prisms"]
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

    if len(rows) != 20:
        problems.append(f"the roster is {len(rows)} configs, not the 20 this cell is built "
                        f"from - five species in four elements")
    if t["plants"] != len(rows):
        problems.append(f"the cell holds {t['plants']} plants for {len(rows)} configs - "
                        f"CAP must stay 1, one specimen of each")
    for family, want in (("Mandelbulb", 16), ("Borromean", 4)):
        got = sum(1 for r in rows if r["family"] == family)
        if got != want:
            problems.append(f"{got} {family} configs, not {want}")

    # §36: a trail-caused Frenzy must always release with the garden intact.
    if L["FrenzyExitVolume"] <= t["volume"]:
        problems.append(f"FrenzyExitVolume {L['FrenzyExitVolume']:,} is at or below the mature "
                        f"garden {t['volume']:,.0f} - a freeze would need active grazing to "
                        f"release, which is a garden that punishes visitors")
    if L["FrenzyExit"] <= t["prisms"]:
        problems.append(f"FrenzyExit count {L['FrenzyExit']:,} is at or below the mature garden "
                        f"{t['prisms']:,} - the same freeze, on the count backstop")

    # §48: Restless EARLY, or the food web is dormant for the whole of the cell's growth.
    if L["RestlessEnterVolume"] >= t["volume"] * 0.5:
        problems.append(f"RestlessEnterVolume {L['RestlessEnterVolume']:,} is over half the "
                        f"mature garden - the food web sleeps while the cell grows")

    # No single specimen may freeze the cell on its own.
    if t["heaviest"] >= L["FrenzyEnterVolume"]:
        problems.append("one specimen alone reaches FrenzyEnterVolume")

    # Collider budget, on the two lines it is actually spent (the /flora skill §6).
    if L["FrenzyEnter"] > LATTICE_FRENZY_ENTER * 1.05:
        problems.append(f"FrenzyEnter count {L['FrenzyEnter']:,} exceeds the Lattice cell's "
                        f"{LATTICE_FRENZY_ENTER:,} - that is this cell's closest shipped sibling "
                        f"and the ceiling it is gated against")
    if t["plants"] > 40:
        problems.append(f"{t['plants']} always-on heart colliders - one per live plant, culled "
                        f"by no phase")

    # The band must clear the nucleus, or ResolvePlantRadius collapses it to one shell.
    if BAND_INNER * MEMBRANE_RADIUS <= NUCLEUS_RADIUS:
        problems.append(f"band inner {BAND_INNER * MEMBRANE_RADIUS:.0f}u is inside the "
                        f"{NUCLEUS_RADIUS:.0f}u nucleus")
    # ...and it must be wide enough that the specimens are not stacked on one another.
    shell = (BAND_OUTER * MEMBRANE_RADIUS) ** 3 - (BAND_INNER * MEMBRANE_RADIUS) ** 3
    occupied = len(rows) * t["widest"] ** 3
    if occupied > shell * 0.10:
        problems.append(f"the {len(rows)} bounding spheres fill {occupied / shell:.1%} of the "
                        f"planting band - specimens will routinely interpenetrate")

    return problems


# ── Asset emission ──────────────────────────────────────────────────────────

HEADER = """%%YAML 1.1
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

FOLDER_META = """fileFormatVersion: 2
guid: %s
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

META = """fileFormatVersion: 2
guid: %s
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  mainObjectFileType: 2
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def flora_asset_name(species, element):
    return f"{PREFIX} {display(species)} {element} Config Data"


def flora_asset(row):
    species, element = row["species"], row["element"]
    name = flora_asset_name(species, element)
    fid, guid = prefab_ref(species)
    return HEADER % (SCRIPT["flora"], name) + (
        f"  FloraPrefab: {{fileID: {fid}, guid: {guid}, type: 3}}\n"
        "  SpawnProbability: 1\n"
        f"  InitialSpawnCount: {INITIAL_SPAWN}\n"
        "  OverrideDefaultPlantPeriod: 0\n"
        "  NewPlantPeriod: 9999999\n"
        f"  PopulationSize: {FLOOR}\n"
        f"  MaxLivePopulation: {CAP}\n"
        f"  GrowthPerOffspring: {row['quota']}\n"
        "  OffspringPerBirth: 1\n"
        f"  ReproductionCooldownSeconds: {COOLDOWN}\n"
        f"  MaturityFraction: {MATURITY}\n"
        f"  OffspringSpread: {SPREAD}\n"
        f"  Element: {ELEMENT_ID[element]}\n"
        "  Variant:\n" + variant_block(species, element))


def fauna_asset():
    """ONE grazer, so the garden is ALIVE rather than a diorama.

    A cell whose populations are bounded by a cap and nothing else is scenery; the food web is
    what makes a specimen's regrowth mean something, and it is the only sanctioned down-force
    on mass (CLAUDE.md's conserved-mass law). Modest on purpose - this cell's subject is the
    sixteen plants, so the crew is a cleanup crew, not a plague.
    """
    name = f"{PREFIX} Tadpole Fauna Config Data"
    fid, guid = TADPOLE_PREFAB
    return HEADER % (SCRIPT["fauna"], name) + (
        f"  FaunaPrefab: {{fileID: {fid}, guid: {guid}, type: 3}}\n"
        "  InitialSpawnCount: 3\n"
        "  PopulationSize: 3\n"
        "  SpawnProbability: 1\n"
        "  FeedsPerOffspring: 20\n"
        "  OffspringPerBirth: 1\n"
        "  ReproductionCooldownSeconds: 10\n"
        "  MaxLivePopulation: 6\n"
        "  CenterFocusBias: 0.35\n"
        "  Element: 2\n"
        "  Variant:\n"
        "    Enabled: 1\n"
        "    HeartWorldScale: 1.737\n"
        "    BaseBodyScale: 0.4\n"
        "    BodyPrismScale: {x: 0.8, y: 0.8, z: 7}\n"
        "    StarvationSeconds: 90\n"
        "    Forager: 1\n"
        "    CohesionRadius: 50\n"
        "    BehaviorUpdateRate: 1.5\n"
        "    TrailBlockInteractionRadius: 20\n"
        "    GoalWeight: 3\n"
        "    MinSpeed: 10\n"
        "    MaxSpeed: 15\n"
        "    OverrideAudio: 0\n"
        "    AudioLoopEvent:\n"
        "      Guid:\n"
        "        Data1: 0\n"
        "        Data2: 0\n"
        "        Data3: 0\n"
        "        Data4: 0\n"
        "      Path: \n"
        "    AudioMinDistance: -1\n"
        "    AudioMaxDistance: -1\n")


def profile_asset(rows):
    name = f"{PREFIX} Cell Spawn Profile"
    # Every row carries its OWN guid - the sixteen this script authors and the four
    # author_borromean_flora_assets.py does - so the list can never disagree with who owns
    # the asset behind an entry.
    floras = "".join(
        f"  - {{fileID: 11400000, guid: {r['guid']}, type: 2}}\n" for r in rows)
    return HEADER % (SCRIPT["profile"], name) + (
        "  FloraExcludeLocalDomain: 0\n"
        "  FloraSpawnVolumeCeiling: 12000\n"
        "  FloraInitialDelaySeconds: 0\n"
        "  FloraSpawnIntervalSeconds: 0\n"
        "  FloraPopulationScale: 1\n"
        "  FloraPlantBudgetScale: 1\n"
        "  SupportedFloras:\n" + floras +
        "  FaunaExcludeLocalDomain: 0\n"
        "  InitialFaunaSpawnWaitTime: 20\n"
        "  FaunaSpawnVolumeThreshold: 1\n"
        "  FaunaPopulationScale: 1\n"
        f"  BaseFaunaSpawnTime: {HEARTBEAT:g}\n"
        "  SeedFullWaveEveryTick: 0\n"
        "  FaunaFoodFloor: 5\n"
        "  FaunaInitialDelaySeconds: 0\n"
        "  FaunaSpawnIntervalSeconds: 0\n"
        "  HerbivoreSpawnPointCount: 3\n"
        "  HerbivoreSpawnRadius: 800\n"
        "  PredatorSpawnPointCount: 0\n"
        "  PredatorSpawnRadius: 900\n"
        "  SupportedFaunas:\n"
        f"  - {{fileID: 11400000, guid: {guid_for(PREFIX + ' Tadpole Fauna Config Data')}, type: 2}}\n")


def cell_asset(rows):
    name = f"{PREFIX} Cell Config"
    L = ladder(rows)
    return HEADER % (SCRIPT["cell"], name) + (
        f"  CellName: {CELL_NAME}\n"
        "  Description: An arboretum - one specimen of each Mandelbulb species and of the\n"
        "    Borromean membrane, in each element; twenty plants and nothing else\n"
        f"  Icon: {{fileID: 21300000, guid: {ICON}, type: 3}}\n"
        "  Difficulty: 2\n"
        "  CellEndGameScore: 0\n"
        f"  MembranePrefab: {{fileID: {MEMBRANE[0]}, guid: {MEMBRANE[1]}, type: 3}}\n"
        f"  NucleusPrefab: {{fileID: {NUCLEUS[0]}, guid: {NUCLEUS[1]}, type: 3}}\n"
        f"  CytoplasmPrefab: {{fileID: {CYTOPLASM[0]}, guid: {CYTOPLASM[1]}, type: 3}}\n"
        "  CellModifiers:\n"
        f"  - {{fileID: {MODIFIER[0]}, guid: {MODIFIER[1]}, type: 3}}\n"
        f"  SpawnProfile: {{fileID: 11400000, guid: {guid_for(PREFIX + ' Cell Spawn Profile')}, type: 2}}\n"
        "  PhaseThresholds:\n" +
        "".join(f"    {k}: {L[k]}\n" for k in (
            "RestlessEnter", "RestlessExit", "FrenzyEnter", "FrenzyExit",
            "RestlessEnterVolume", "RestlessExitVolume",
            "FrenzyEnterVolume", "FrenzyExitVolume")))


def emit(rows):
    """name -> (text, guid). Every asset this cell owns."""
    out = {}
    for r in rows:
        if not r["owned_here"]:
            continue          # the Borromean four belong to their own species' generator
        n = r["asset"]
        out[n] = (flora_asset(r), guid_for(n))
    n = f"{PREFIX} Tadpole Fauna Config Data"
    out[n] = (fauna_asset(), guid_for(n))
    n = f"{PREFIX} Cell Spawn Profile"
    out[n] = (profile_asset(rows), guid_for(n))
    n = f"{PREFIX} Cell Config"
    out[n] = (cell_asset(rows), guid_for(n))
    return out


def folder_meta():
    """The FOLDER's own .meta, which is not optional.

    Without one Unity mints a fresh GUID for the directory on every clone, so two machines
    disagree about the folder's identity and anything that ever references it dangles. The
    Garland cell's folder is missing its own and is the standing example."""
    return CELL_DIR + ".meta", FOLDER_META % guid_for("folder")


# ── The Cell Selector's list ────────────────────────────────────────────────

def scene_state():
    """Is this cell in Menu_Main's Cell.CellConfigs, and at what size?

    CellSelectorToy AUTHORS NO CELL LIST - it reads Cell.AvailableConfigs, which is that
    serialized field. So adding a world to the selector is an edit to the cell's own config
    rotation and to nothing else (Docs/ToySystem/ARCHITECTURE.md).
    """
    text = read(MENU_SCENE)
    m = re.search(r"propertyPath: CellConfigs\.Array\.size\n      value: (\d+)", text)
    size = int(m.group(1)) if m else -1
    return size, guid_for(PREFIX + " Cell Config") in text


def scene_patch(text, guid):
    """Append one entry to CellConfigs, preserving every existing one."""
    m = re.search(r"( *- target: \{fileID: (\d+), guid: ([a-f0-9]{32}),\n"
                  r" *type: 3\}\n *propertyPath: CellConfigs\.Array\.size\n"
                  r" *value: )(\d+)(\n)", text)
    if not m:
        raise SystemExit("Menu_Main: no CellConfigs.Array.size override to extend")
    size = int(m.group(4))
    target_fid, target_guid = m.group(2), m.group(3)
    text = text[:m.start(4)] + str(size + 1) + text[m.end(4):]

    last = None
    for hit in re.finditer(r" *- target: \{fileID: \d+, guid: [a-f0-9]{32},\n"
                           r" *type: 3\}\n *propertyPath: 'CellConfigs\.Array\.data\[\d+\]'\n"
                           r" *value: \n *objectReference: \{fileID: 11400000, guid: [a-f0-9]{32},\n"
                           r" *type: 2\}\n", text):
        last = hit
    if last is None:
        raise SystemExit("Menu_Main: no CellConfigs entry to append after")
    entry = (f"    - target: {{fileID: {target_fid}, guid: {target_guid},\n"
             f"        type: 3}}\n"
             f"      propertyPath: 'CellConfigs.Array.data[{size}]'\n"
             f"      value: \n"
             f"      objectReference: {{fileID: 11400000, guid: {guid},\n"
             f"        type: 2}}\n")
    return text[:last.end()] + entry + text[last.end():]


# ── Report ──────────────────────────────────────────────────────────────────

def report(rows):
    t, L = totals(rows), ladder(rows)
    print(f"\n{CELL_NAME} — {len(rows)} specimens, one of each species in each element")
    print("  the whole environment; no EnvironmentPrefab, no second producer\n")
    print("  species          element    prisms  curves      volume     across   per-prism")
    for r in rows:
        lo, hi = r["dims"]
        curves = f"{r['curves']:>5}" if r["curves"] else "    —"
        print(f"  {r['display']:<16} {r['element']:<9} {r['prisms']:>6}  "
              f"{curves}  {r['volume']:>10,.0f}  {2 * r['radius']:>7.1f}u  "
              f"{lo:>5.2f}..{hi:>6.2f}")
    print(f"\n  mature garden   {t['prisms']:,} prisms, {t['volume']:,.0f} volume, "
          f"{t['plants']} plants")
    for family in ("Mandelbulb", "Borromean"):
        fam = [r for r in rows if r["family"] == family]
        print(f"    {family:<12}  {sum(r['prisms'] for r in fam):>6,} prisms, "
              f"{sum(r['volume'] for r in fam):>9,.0f} volume, {len(fam)} plants"
              + ("" if family == "Mandelbulb"
                 else "   (configs owned by author_borromean_flora_assets.py)"))
    print(f"  colliders       {t['plants']} always-on heart crystals (one per live plant, "
          f"culled by no phase) — the Lattice cell's is {LATTICE_HEART_COLLIDERS:,}")
    print(f"                  {t['prisms']:,} LOD-cullable prisms at maturity, ceiling "
          f"{L['FrenzyEnter']:,} — Atlantis is {ATLANTIS_PRISMS:,} and the Lattice cell's "
          f"ceiling is {LATTICE_FRENZY_ENTER:,}")
    print(f"  band            {BAND_INNER * MEMBRANE_RADIUS:.0f}u..{BAND_OUTER * MEMBRANE_RADIUS:.0f}u "
          f"(volume-uniform), widest specimen {2 * t['widest']:.0f}u across")
    print("\n  ladder")
    for k in ("RestlessEnter", "RestlessExit", "FrenzyEnter", "FrenzyExit",
              "RestlessEnterVolume", "RestlessExitVolume",
              "FrenzyEnterVolume", "FrenzyExitVolume"):
        ratio = L[k] / (t["volume"] if "Volume" in k else t["prisms"])
        print(f"    {k:<22} {L[k]:>10,}   {ratio:.2f}x mature")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    rows = budget()
    report(rows)

    problems = verify(rows) + borromean_state(rows)
    assets = emit(rows)
    size, listed = scene_state()

    if args.check:
        drift = []
        for name, (text, guid) in assets.items():
            path = os.path.join(CELL_DIR, name + ".asset")
            if not os.path.exists(path) or read(path) != text:
                drift.append(path)
            meta = path + ".meta"
            if not os.path.exists(meta) or read(meta) != META % guid:
                drift.append(meta)
        fpath, ftext = folder_meta()
        if not os.path.exists(fpath) or read(fpath) != ftext:
            drift.append(fpath)
        if not listed:
            drift.append(f"{MENU_SCENE} (Cell.CellConfigs, size {size})")
        if drift or problems:
            print("\nFAIL")
            for p in problems:
                print(f"  - {p}")
            for d in drift:
                print(f"  - differs from what this script authors: {d}")
            return 1
        print("\nOK — every asset matches the model, and the cell is in the Cell Selector.")
        return 0

    if problems:
        print("\nFAIL — refusing to write:")
        for p in problems:
            print(f"  - {p}")
        return 1

    os.makedirs(CELL_DIR, exist_ok=True)
    fpath, ftext = folder_meta()
    with open(fpath, "w", encoding="utf-8") as fh:
        fh.write(ftext)
    for name, (text, guid) in assets.items():
        path = os.path.join(CELL_DIR, name + ".asset")
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(text)
        with open(path + ".meta", "w", encoding="utf-8") as fh:
            fh.write(META % guid)
    print(f"\nwrote {len(assets)} assets to {os.path.relpath(CELL_DIR, REPO)}")

    if listed:
        print("  Cell Selector: already listed in Menu_Main")
    else:
        text = read(MENU_SCENE)
        with open(MENU_SCENE, "w", encoding="utf-8") as fh:
            fh.write(scene_patch(text, guid_for(PREFIX + " Cell Config")))
        print(f"  Cell Selector: appended to Menu_Main's Cell.CellConfigs (size {size} -> {size + 1})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
