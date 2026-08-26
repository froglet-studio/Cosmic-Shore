#!/usr/bin/env python3
"""
Author the flora POPULATION model onto every flora config asset a SpawnProfile references.

Why a script and not 25 hand-edits: the numbers are DERIVED, and a ladder that is hand-typed
drifts the first time one cell is retuned. This holds the model, prints the table it produces,
and supports --check so CI (or a reviewer) can prove the assets still match it.
See Docs/ECOSYSTEM.md §32 and the /ecology skill §7.1.

THE MODEL
---------
Every flora species gets the fauna-shaped pair (seed floor + hard cap) plus a reproduction
quota funded by GROWTH. Two rules decide the numbers:

1. floor  = InitialSpawnCount
   The authored opening density, unchanged. The cell opens exactly as it does today; the
   seeder's new job is only to hold that floor against extinction.

2. cap    = the species' plant count AT ITS AUTHORED MASS.
   - LATTICE species (gyroid): the conversion this branch exists for. One big plant became
     many small ones of the SAME TOTAL MASS, so cap = old_single_plant_budget / UNIT_CELL.
   - Everyone else: their per-plant budget is unchanged, so every extra plant is extra MASS -
     cap = ISC x 1.5 gives reproduction room to refill grazed gaps without letting a species
     multiply the cell's authored flora mass. The plant count was previously UNBOUNDED (the
     spawner planted one every plant period forever); this bounds it for the first time.
   Every cap is then clamped to MAX_PLANTS_PER_SPECIES, because a cap is a crystal count (see
   below). The cap is a PERFORMANCE backstop either way - the cell's Frenzy volume gate is still
   what actually bounds the standing forest (Docs/ECOSYSTEM.md §5.1). It stops the count running
   away; it does not set the forest's size.

3. GrowthPerOffspring
   - LATTICE: the whole unit cell. "Complete yourself, then extend the lattice" IS the mechanic.
   - Everyone else: 35% of the per-plant budget, so a plant colonises several times over its
     life rather than once.

   NOTE: what this file writes is the SPECIES baseline. THE TIME LAW (Docs/ECOSYSTEM.md 38)
   scales it by the plant's ELEMENT at spawn - Time breeds at 1.25x the fleet rate, the other
   three at 0.8x. That lives in code (Flora.ResolveGrowthPerOffspring) and NOT here, because
   this field is authored per CONFIG while the element is ROLLED per plant, and every species
   that actually spends this quota sets SpreadElements with a four-element palette - so a
   per-element fork in this script would reach none of them. Do not add one; the same law
   reaches the lattice species through their colony CYCLE PERIOD instead
   (AssembledFlora.ColonyCyclePeriod), since the quota below is inert for them.

Nothing here removes anything. A lowered cap stops PRODUCTION; it never culls a live plant
(Docs/ECOSYSTEM.md §0).

Usage:
    python3 Tools/Build/author_flora_populations.py            # write + print the table
    python3 Tools/Build/author_flora_populations.py --check    # verify, exit 1 on drift
"""

import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SO = os.path.join(REPO, "Assets", "_SO_Assets")

# ── The gyroid unit cell ──────────────────────────────────────────────────────
# Measured off the assembler's OWN bond table (48 entries, 12 block types) by walking the
# lattice exactly as AssembledFlora.PreviewGyroid does: 3 bonds out from a seed is 27 prisms
# and is the smallest patch that contains all 12 block types - one of everything the assembler
# can say. Extent ~40 x 42 x 12 world units at separationDistance 3. Bond length ~7.8u; the
# lattice is BCC with a ~119.6u conventional cube (~584 prisms), so a unit cell in the
# CRYSTALLOGRAPHIC sense would be ~292 prisms - far too big to make a population out of.
UNIT_CELL = 27

# The octagon patch (Docs/ECOSYSTEM.md §32.2, measured): the gyroid's four danger types close
# into 8-prism rings, one crystal per ring, and each ring owns 24 prisms of the surface
# (8 danger / one-third danger fraction). This replaced UNIT_CELL as the lattice conversion
# basis when the octagon colony landed - a plant IS an octagon-owner now.
OCTAGON_PATCH = 24

# Non-lattice colonisation: reproduction gives a species room to regrow and fill gaps, but it
# must NOT be able to multiply the cell's authored flora mass - their per-plant budget is
# unchanged, so every extra plant is extra mass. 1.5x the authored opening density is headroom
# without a re-tune; the Frenzy gate is still what actually stops growth.
COLONISE = 1.5

# Per-species plant ceiling, and the reason this file talks about colliders: every plant is a
# lifeform, so it carries ONE heart crystal whose collider is always on and is NOT phase-LOD
# culled (Docs/ECOSYSTEM.md §21.6). 'cap' therefore IS a crystal count. 60 keeps any one species
# to a legible share of the per-cell collider budget (MASTERPLAN §4). This is THE dial to turn if
# a cell reads as too busy.
MAX_PLANTS_PER_SPECIES = 60

QUOTA_FRACTION = 0.35   # non-lattice GrowthPerOffspring = budget x this ...
QUOTA_MAX = 400         # ... but never so large that a big plant can never afford a child
COOLDOWN = 5
MATURITY = 0.5

# Minimum seed floor for a LATTICE species. A colony inherits its founder's variant pick (its
# ELEMENT), exactly as a fauna lineage does - so a species seeded from ONE plant would grow a
# whole cell of one element and quietly waste the config's authored element spread. Four
# independent founders keep several elements in the cell, and double as the extinction floor.
# (The pick used to carry a spawn LEVEL too. Lifeform levels are retired outright - a lifeform
# is its species and its ELEMENT, nothing else, Docs/ECOSYSTEM.md §39 - and a lattice plant's
# leaf never changes size anyway (Flora.PrismSizeFixedByGrowthRule). So the founders are now
# the ONLY thing that varies inside a colony, which is precisely why the floor below matters.)
LATTICE_MIN_FOUNDERS = 4

# Species whose growth rule is a LATTICE: an offspring is handed a real bond site off the
# parent's own frontier (AssembledFlora.TryResolveOffspringPlacement), so many small plants
# add up to one continuous minimal surface. Matched on the flora prefab.
LATTICE_PREFABS = {"GyroidFlora.prefab", "SchwarzPFlora.prefab", "QuasicrystalFlora.prefab"}

# Per-plant budget for a lattice species: the 24-prism octagon patch plus headroom for the
# boundary prisms the ownership epsilon lets a plant win (measured patches run 22-28; the
# ownership gate, not this budget, is the real bound - see AssembledFlora.OwnsLatticeSite).
LATTICE_BUDGET = 30

# Per-species unit cell and per-plant budget. The two lattice species do NOT share these:
# a gyroid plant owns a 24-prism octagon and is given 30 for the boundary prisms its
# ownership epsilon lets it win, while a Schwarz P plant owns one TILE - an exact integer
# partition of the surface (Docs/ECOSYSTEM.md 34) - so its patch is exactly
# SchwarzPTileData.SiteCount(level) and it needs no headroom at all: there is no contested
# boundary to win. 36 is the level the shipped separation 6 / periodScale 60 resolves to.
LATTICE_PATCH = {
    "GyroidFlora.prefab":   (OCTAGON_PATCH, LATTICE_BUDGET),   # 24 owned, 30 budget
    "SchwarzPFlora.prefab": (36, 36),                          # 36 owned, 36 budget - exact
    # Quasicrystal: a plant owns ONE HEART's tree territory on the icosahedral
    # quasilattice (Docs/ECOSYSTEM.md 37). Tree cells are exact but their size
    # legitimately varies - measured 44..97 struts, mean 58.8, over 1,461 simulated
    # plants (Tools/Build/measure_icosahedral_quasilattice.py) - so the conversion
    # basis is the MEAN and the budget sits above the measured MAX, because the
    # ownership tree, not the budget, is the real bound: a plant whose cell the
    # budget truncated would leave permanent holes in the colony's scaffold.
    "QuasicrystalFlora.prefab": (59, 110),                     # 59 mean owned, 110 budget
}

# No per-CONFIG override is needed, and that is the point of FloraVariantTuning.LatticeScale.
# Space authors a wider lattice, but it scales periodScale and separationDistance TOGETHER, so
# SchwarzPTileData.ResolveLevel returns its peers' level unchanged: 36 sites per tile, the same
# mesh, the same prism count. Only the distances grow. (An earlier pass scaled separation alone,
# which re-resolved to 6 sites and did need an override here - that is exactly the topology
# change LatticeScale exists to avoid, and the override going away is the evidence it worked.)

# INERT for lattice species since reproduction became a POPULATION event (Docs/ECOSYSTEM.md
# §32.7, organic-growth pass): the colony births ONE plant per fauna-wave period at a random
# open octagon from GyroidColonyFrontier, so the per-plant quota/cooldown/per-birth fields are
# never what decides a lattice birth - AssembledFlora's placement override only ever returns a
# site the population cycle staged. They are still WRITTEN (a species that ever falls back to
# the per-plant path must carry sane values, and 'unset' is not a state a serialized int has),
# but the real cadence dial is the cell profile's BaseFaunaSpawnTime. Per-birth is 1 to match
# what the colony actually does; the retired value was 8 ("plant into every free neighbouring
# octagon at once"), which is precisely the breadth-first shell the population model replaced.
LATTICE_QUOTA = 22
LATTICE_OFFSPRING_PER_BIRTH = 1

# Hand-authored test-cell configs the model must NOT manage. Empty since the Gyroid Lab was
# retired (2026-08-16): a deliberately uncapped, guardrail-free laboratory cell answers
# questions about the growth RULE, and once the rule was right it only answered questions
# about itself - the shipped biomes are the honest test. Keep the hook: the next lab goes here.
EXCLUDE = set()

# Configs owned by ANOTHER authoring script, by asset-name prefix. Not an exclusion: it is a
# hand-off, and it prints who the owner is. Two scripts silently writing one field is a
# run-order hazard that only surfaces months later when somebody re-runs the older tool
# (Docs/ECOSYSTEM.md 34.5 / the /ecology skill's "two fitters must not own one asset").
#
# "Lattice ": the Lattice cell (Docs/ECOSYSTEM.md 36). Its eight colonies are not a conversion
# of an authored plant - the cell's whole environment IS the colony - so this file's rule
# (cap = old_single_plant_budget / patch) has no input to work from and would silently shrink
# them back to the Blob caps.
OWNED_ELSEWHERE = {
    "Lattice ": "Tools/Build/author_lattice_cell.py",
}


def owner_of(name):
    for prefix, script in OWNED_ELSEWHERE.items():
        if name.startswith(prefix):
            return script
    return None

# The AUTHORED single-plant budget each lattice config carried BEFORE the conversion. Recorded
# explicitly rather than read back off the asset, because the conversion overwrites that value -
# deriving the cap from the live file would make a second run compute cap = 27/27 = 1 and quietly
# delete the population. It is also the audit trail: cap x UNIT_CELL should reproduce this number,
# which is the whole claim of the conversion (same mass, many plants).
LATTICE_SOURCE_BUDGET = {
    "Blob Mass Gyroid Flora Config Data": 1500,
    "Blob Space Gyroid Flora Config Data": 800,
    "Blob Time Gyroid Flora Config Data": 1000,
    "Wildlife Cell 4 Mass Gyroid Flora Config Data": 1500,
    "Wildlife Cell 4 Space Gyroid Flora Config Data": 800,
    "Wildlife Cell 4 Time Gyroid Flora Config Data": 1000,
    "Hesperides Gyroid Topiary Config Data": 190,
    "Gyroid Flora Charge": 1000,
    "Gyroid Flora Mass": 1500,
    "Gyroid Flora Space": 800,
    "Gyroid Flora Time": 1000,
    # Schwarz P: the prefab's authored 800 (the four element assets keep the prefab value with
    # the -1 sentinel, so 800 is what each of them actually ran with) and the topiary's 150.
    "Blob SchwarzP Flora Config Data": 800,
    "Hesperides SchwarzP Topiary Config Data": 150,
    "SchwarzP Flora Charge": 800,
    "SchwarzP Flora Mass": 800,
    "SchwarzP Flora Space": 800,
    "SchwarzP Flora Time": 800,
    # Quasicrystal: the prefab's authored 800, same convention as Schwarz P.
    # cap = round(800 / 59) = 14 plants - 14 always-on heart colliders at cap,
    # under Schwarz P's 22 because a star plant carries ~59 struts to a tile's 36.
    "Blob Quasicrystal Flora Config Data": 800,
    "Quasicrystal Flora Charge": 800,
    "Quasicrystal Flora Mass": 800,
    "Quasicrystal Flora Space": 800,
    "Quasicrystal Flora Time": 800,
}


def read(path):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        return fh.read()


def build_guid_map():
    guids = {}
    for root, _, files in os.walk(os.path.join(REPO, "Assets")):
        for name in files:
            if not name.endswith(".meta"):
                continue
            path = os.path.join(root, name)
            match = re.search(r"guid: ([a-f0-9]{32})", read(path))
            if match:
                guids[match.group(1)] = path[:-5]
    return guids


def script_guid(rel):
    return re.search(r"guid: ([a-f0-9]{32})", read(os.path.join(REPO, rel) + ".meta")).group(1)


def prefab_budgets():
    out = {}
    for root, _, files in os.walk(os.path.join(REPO, "Assets", "_Prefabs", "FloraAndFauna")):
        for name in files:
            if not name.endswith(".prefab"):
                continue
            match = re.search(r"maxTotalSpawnedObjects: (\d+)", read(os.path.join(root, name)))
            if match:
                out[name] = int(match.group(1))
    return out


def collect_targets(guids, defaults):
    """Every flora config a SpawnProfile references, plus the shared per-element palette assets."""
    profile_guid = script_guid("Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs")
    flora_guid = script_guid("Assets/_Scripts/Utility/DataContainers/FloraConfigurationSO.cs")

    referenced = []
    for root, _, files in os.walk(SO):
        for name in sorted(files):
            if not name.endswith(".asset"):
                continue
            path = os.path.join(root, name)
            text = read(path)
            if f"guid: {profile_guid}" not in text or "SupportedFloras:" not in text:
                continue
            block = text.split("SupportedFloras:")[1].split("SupportedFaunas:")[0]
            for g in re.findall(r"guid: ([a-f0-9]{32})", block):
                target = guids.get(g)
                if target and target not in referenced:
                    referenced.append(target)

    # The shared per-element species assets (_SO_Assets/Lifeforms) are the ELEMENT PALETTE and
    # the Lifeform Matrix toy's source. A lattice species' budget is its element identity, so the
    # unit-cell shrink has to land there too or the toy keeps spawning the old 1000-prism plant.
    lifeforms = os.path.join(SO, "Lifeforms")
    for name in sorted(os.listdir(lifeforms)):
        if not name.endswith(".asset"):
            continue
        path = os.path.join(lifeforms, name)
        if f"guid: {flora_guid}" in read(path) and is_lattice(path, guids) and path not in referenced:
            referenced.append(path)
    return referenced


def prefab_name(path, guids):
    match = re.search(r"FloraPrefab: \{fileID: -?\d+, guid: ([a-f0-9]{32})", read(path))
    if not match:
        return "?"
    return os.path.basename(guids.get(match.group(1), "?"))


def is_lattice(path, guids):
    return prefab_name(path, guids) in LATTICE_PREFABS


def effective_budget(path, guids, defaults):
    text = read(path)
    override = re.search(r"MaxTotalSpawnedObjectsOverride: (-?\d+)", text)
    if override and int(override.group(1)) >= 0:
        return int(override.group(1))
    variant = re.search(r"MaxTotalSpawnedObjects: (-?\d+)", text)
    if variant and int(variant.group(1)) >= 0:
        return int(variant.group(1))
    return defaults.get(prefab_name(path, guids), 400)


def plan(path, guids, defaults):
    text = read(path)
    isc = int(re.search(r"InitialSpawnCount: (\d+)", text).group(1))
    lattice = is_lattice(path, guids)
    old_budget = effective_budget(path, guids, defaults)

    if lattice:
        name = os.path.basename(path)[:-6]
        if name not in LATTICE_SOURCE_BUDGET:
            raise SystemExit(
                f"{name}: lattice config with no recorded pre-conversion budget. Add it to "
                f"LATTICE_SOURCE_BUDGET (its authored MaxTotalSpawnedObjects) - the conversion "
                f"overwrites that field, so it cannot be recovered from the asset.")
        old_budget = LATTICE_SOURCE_BUDGET[name]
        patch, budget = LATTICE_PATCH[prefab_name(path, guids)]
        # Same mass, many plants: this is the conversion, stated as arithmetic. The divisor is
        # the PATCH (what a plant actually settles at), so cap x patch = the old single-plant
        # mass.
        cap = max(1, round(old_budget / patch))
        quota = LATTICE_QUOTA
    else:
        budget = old_budget
        cap = max(isc + 2, round(isc * COLONISE))
        quota = min(QUOTA_MAX, max(8, round(budget * QUOTA_FRACTION)))

    cap = min(cap, MAX_PLANTS_PER_SPECIES)

    floor = max(1, isc)
    if lattice:
        floor = min(cap, max(floor, LATTICE_MIN_FOUNDERS))
    return dict(floor=floor, cap=max(cap, floor), quota=quota,
                budget=budget, old_budget=old_budget, lattice=lattice)


FIELDS = ("PopulationSize", "MaxLivePopulation", "GrowthPerOffspring",
          "OffspringPerBirth", "ReproductionCooldownSeconds", "MaturityFraction",
          "OffspringSpread")


def render(p):
    spread = 40 if p["lattice"] else 60
    per_birth = LATTICE_OFFSPRING_PER_BIRTH if p["lattice"] else 1
    return (f"  PopulationSize: {p['floor']}\n"
            f"  MaxLivePopulation: {p['cap']}\n"
            f"  GrowthPerOffspring: {p['quota']}\n"
            f"  OffspringPerBirth: {per_birth}\n"
            f"  ReproductionCooldownSeconds: {COOLDOWN}\n"
            f"  MaturityFraction: {MATURITY}\n"
            f"  OffspringSpread: {spread}\n")


def apply(path, p, check):
    text = read(path)
    original = text

    # Strip any previous run's fields so the script is idempotent.
    for field in FIELDS:
        text = re.sub(rf"^  {field}: .*\n", "", text, flags=re.M)

    # Insert in DECLARATION order (after NewPlantPeriod, before PreferredSites/Element) - Unity
    # matches serialized fields by name so this is cosmetic, but a file that reads in class
    # order is a file a human can diff.
    anchor = re.search(r"^  NewPlantPeriod: .*\n", text, flags=re.M)
    if not anchor:
        return None, "no NewPlantPeriod anchor"
    text = text[:anchor.end()] + render(p) + text[anchor.end():]

    # The lattice budget lives in the Variant block (the element's identity).
    if p["lattice"]:
        text = re.sub(r"(\n    MaxTotalSpawnedObjects: )-?\d+",
                      rf"\g<1>{p['budget']}", text, count=1)

    if text == original:
        return False, None
    if not check:
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(text)
    return True, None


def main():
    check = "--check" in sys.argv
    guids = build_guid_map()
    defaults = prefab_budgets()
    targets = collect_targets(guids, defaults)

    print(f"{'config':<50}{'kind':<9}{'budget':>12}{'floor':>7}{'cap':>6}{'quota':>7}{'prisms@cap':>12}")
    print("-" * 103)
    drift, total_plants, total_prisms = [], 0, 0
    handed_off = []
    for path in targets:
        name = os.path.basename(path)[:-6]
        if name in EXCLUDE:
            continue
        owner = owner_of(name)
        if owner:
            handed_off.append((name, owner))
            continue
        p = plan(path, guids, defaults)
        changed, err = apply(path, p, check)
        if err:
            print(f"!! {os.path.basename(path)}: {err}")
            continue
        if changed:
            drift.append(os.path.basename(path))
        kind = "lattice" if p["lattice"] else "-"
        budget = (f"{p['old_budget']}->{p['budget']}" if p["lattice"] else str(p["budget"]))
        prisms = p["cap"] * p["budget"]
        total_plants += p["cap"]
        total_prisms += prisms
        print(f"{os.path.basename(path)[:-6]:<50}{kind:<9}{budget:>12}"
              f"{p['floor']:>7}{p['cap']:>6}{p['quota']:>7}{prisms:>12}")

    print("-" * 103)
    print(f"{'TOTAL (all cells, at cap)':<50}{'':<9}{'':>12}{'':>7}"
          f"{total_plants:>6}{'':>7}{total_prisms:>12}")
    if handed_off:
        print("\nHANDED OFF (owned by another authoring script, untouched here):")
        for name, owner in handed_off:
            print(f"  {name:<50} -> {owner}")

    print("\nNOTE: 'prisms@cap' is a per-species ceiling, not a prediction - the cell's Frenzy\n"
          "volume gate stops growth first in every shipped biome. Each plant also carries ONE\n"
          "heart crystal (an always-on collider), so 'cap' IS the crystal count that species\n"
          "can reach: that is the collider line to watch, and MaxLivePopulation is its dial.")

    if check and drift:
        print(f"\nDRIFT: {len(drift)} asset(s) do not match the model:")
        for name in drift:
            print("  " + name)
        return 1
    if not check:
        print(f"\nwrote {len(drift)} asset(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
