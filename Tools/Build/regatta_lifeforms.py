#!/usr/bin/env python3
"""
LIFEFORMS AS A REGATTA COURSE SETTING (Plan step: time-arena-course).

Garrett, 2026-11-06: perturb Regatta "in much more interesting ways ... lifeforms, etc." This module is the one
description of what a course's lifeforms ARE, read by the asset generator (author_regatta_assets.py, which writes
them) and by any course search (which moves them):

  WHERE    planting sites per leg beside the spine, how far out, on which side, how far from the rings
           (serialized on SpawnableRegattaRails as RegattaLifeformSiteSettings; the C# RegattaLifeformSites sows
           them) and the fauna corridor round the spine (0 = creatures may cross the racing line).
  WHICH    the plant species and creature species (a fixed catalogue, below).
  HOW MANY plants per species and creatures per species (the per-course species configs and spawn profile).

`NONE` is today's course exactly: no sites, no corridor, the Barren spawn profile, so author_regatta_assets.py emits
the same cell configs it always did and `--check` is green with nothing re-tuned.

THE ECOLOGY LAW (Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md, LOCKED) is kept by construction, not by care:
  - the arena never spawns a lifeform. It prepares GROUND; the cell's ordinary spawner plants there (the garden
    contract, Cell.TryTakePlantingSite), so plants grow in, are grazed, wither to their hearts and are re-seeded by
    the same rules as everywhere. Nothing pops in or out, nothing is culled, nothing has a lifespan.
  - plants roll Jade / Ruby / Gold uniformly (the spawner's job) and creatures take the cell's controlling colour.
  - super-shielded mass is never food: the rails stay untouchable; grazers eat the plants.
  - volume is the spine: the cell's phase ladder is raised by the standing forest's MEASURED volume (read from the
    shipped species assets, never transcribed), so a forest never pins the cell at Frenzy.

THE NETWORK RULE (Skein's and Regatta's reason for no food web: "a race whose obstacles differ per machine is not a
race"): every plant species here is NetworkSynced (FloraNetworkSync replicates the planting DECISION - species, root
pose, domain, element), and the sites themselves are a pure function of the course seed. What still differs per
peer is a plant's grown SHAPE (growth is local by design, FloraNetworkSync's fidelity contract) and, for creatures,
whatever FaunaNetworkSync does not carry. Creatures are opted in to FaunaNetworkSync here; that is a rollout step
for the netcode owner to check (Docs/ECOSYSTEM_NETWORK_SYNC.md §5), and creatures default to none.

WHAT THE OFFLINE MODEL CAN SEE: NOTHING. race_models.py has no lifeform term and this module adds none on purpose:
a plant's cost to a hull (a Rhino's sword, a Squirrel's skim on its prisms, a Manta threading branches, a heart's
crystal a human collects and the AI never detours for) is real-game only. A search may carry these dimensions as
DESCRIPTORS (descriptors()), never as a scored term; every lifeform course is judged in the real game.

    python3 Tools/Build/regatta_lifeforms.py --self-test     # renders, validates, negative controls
    python3 Tools/Build/regatta_lifeforms.py --describe      # the catalogue and the presets, priced
"""
import dataclasses
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
VERSION = 1

# ── The catalogue: what may live in a Regatta cell ───────────────────────────
# Plants: Tollway's networked race-court species (the same three non-lattice families Thresher Time also lines its
# track with), COPIED per course with only the counts changed - never edited, so neither mode is touched. No
# lattice species (Gyroid, Schwarz P, Quasicrystal): a lattice colony births its next plant at its FRONTIER, not on
# a prepared site, so it would grow wherever its colony walks - across the racing line included.
TOLLWAY_CELL_DIR = "Assets/_SO_Assets/Cell Configs/Tollway Cell"
SKIM_CELL_DIR = "Assets/_SO_Assets/Cell Configs/Skim Race Cell"
CELL_DIR = "Assets/_SO_Assets/Cell Configs/Regatta Cell"
LIFEFORM_DIR = "Assets/_SO_Assets/Lifeforms"
FLORA_PREFAB_DIR = "Assets/_Prefabs/FloraAndFauna"
ELEMENTS = ("Charge", "Mass", "Space", "Time")
FLORA_SOURCES = {sp: f"{TOLLWAY_CELL_DIR}/Tollway Anchor Flora {sp}.asset" for sp in ("Spire", "Cacti", "Borromean")}
# Creatures: the Skim Race tadpole (a grazer - it eats the plants, so a grazed forest thins and re-seeds on its own).
# Its prefab carries the full FaunaNetworkSync rig (Docs/ECOSYSTEM_NETWORK_SYNC.md §5).
FAUNA_SOURCES = {"Tadpole": f"{SKIM_CELL_DIR}/Skim Race Tadpole Fauna Config Data.asset"}

# The rails' lane offset (RegattaCourse.DefaultRails.LaneOffset, asserted by author_regatta_assets.py) and the C#
# floor above it (RegattaLifeformSites.MinRailClearance, asserted against the C# below).
LANE_OFFSET = 22.0
MIN_RAIL_CLEARANCE = 30.0
RINGS_PER_LAP = 8

# Budgets a lifeform course must stay inside: the forest is scenery a pilot reads at 300 u/s, not a second arena.
MAX_PLANTS = 48              # all species together
MAX_FOREST_PRISMS = 6000     # the standing forest's prisms (about a third of an I4 rail, which costs no colliders)
MAX_CREATURES = 25           # live cap over all creatures - Shombith's number for Serpentine's grid
MAX_OFFSET = 400.0           # u from the spine; farther is not "along the course" any more
SITE_MARGIN = 0.75           # plants / sites asked for, at most (see validate)


@dataclasses.dataclass(frozen=True)
class Lifeforms:
    """One course's lifeforms. Every default is today's course."""
    plants: tuple = ()                    # ((species, count), ...)
    creatures: tuple = ()                 # ((creature, count), ...): count = seed floor = live cap
    sites_per_leg: tuple = ()             # one int per leg, gate order; () = no sites
    offset_min: float = 90.0
    offset_max: float = 160.0
    side_deg: float = 0.0                 # 0 outward from the cell centre, 180 inward, 90/270 across the plane
    side_spread_deg: float = 180.0        # half-width; 180 = all round the spine
    leg_window: tuple = (0.2, 0.8)
    ring_clearance: float = 60.0
    fauna_corridor: float = 0.0           # 0 = no corridor
    fauna_standoff: float = 50.0

    @property
    def is_none(self) -> bool:
        return not self.plants and not self.creatures and not any(self.sites_per_leg) and self.fauna_corridor <= 0

    @property
    def plant_count(self) -> int:
        return sum(n for _, n in self.plants)

    @property
    def creature_count(self) -> int:
        return sum(n for _, n in self.creatures)

    @property
    def site_count(self) -> int:
        return sum(max(0, n) for n in self.sites_per_leg)


NONE = Lifeforms()

# Named settings to fly (none ships; a course entry or the search picks one). Each is a different KIND of course.
PRESETS = {
    # Markers on the straights, outside the circuit: a pilot reads the line by them and nothing crosses it.
    "groves": Lifeforms(plants=(("Spire", 16),), sites_per_leg=(3,) * RINGS_PER_LAP,
                        offset_min=110.0, offset_max=180.0, side_deg=0.0, side_spread_deg=70.0),
    # A thicket all round the line, close: plants a hull brushes, a Rhino can cut, a Squirrel can skim.
    "thicket": Lifeforms(plants=(("Cacti", 12), ("Spire", 12)), sites_per_leg=(4,) * RINGS_PER_LAP,
                         offset_min=60.0, offset_max=120.0, side_spread_deg=180.0),
    # A grazed reef off one side, with tadpoles that may cross the racing line to reach it.
    "reef": Lifeforms(plants=(("Borromean", 6), ("Spire", 10)), creatures=(("Tadpole", 8),),
                      sites_per_leg=(2, 4, 2, 4, 2, 4, 2, 4), offset_min=120.0, offset_max=260.0,
                      side_deg=180.0, side_spread_deg=60.0),
    # The same reef, its creatures kept off the line by a corridor.
    "reef-fenced": Lifeforms(plants=(("Borromean", 6), ("Spire", 10)), creatures=(("Tadpole", 8),),
                             sites_per_leg=(2, 4, 2, 4, 2, 4, 2, 4), offset_min=120.0, offset_max=260.0,
                             side_deg=180.0, side_spread_deg=60.0, fauna_corridor=110.0),
}

# ── Search space (for the course search: DESCRIPTORS ONLY, the model scores none of this) ─────────────────────
# Each dimension is (name, low, high). decode() turns a point in [0,1]^n into a Lifeforms that validates.
SPACE = (
    ("density", 0.0, 6.0),             # sites per leg, mean
    ("leg_bias", -1.0, 1.0),           # >0 crowds the odd legs, <0 the even ones (a lap with thick and open legs)
    ("offset_min", LANE_OFFSET + MIN_RAIL_CLEARANCE, 220.0),
    ("offset_depth", 20.0, 180.0),     # offset_max - offset_min
    ("side_deg", 0.0, 360.0),
    ("side_spread_deg", 30.0, 180.0),
    ("fill", 0.4, 1.0),                # plants / sites
    ("spire_share", 0.0, 1.0),
    ("cacti_share", 0.0, 1.0),
    ("borromean_share", 0.0, 1.0),
    ("creatures", 0.0, 12.0),
    ("fauna_corridor", 0.0, 160.0),
)


def decode(x) -> Lifeforms:
    """A point in the unit cube -> a Lifeforms that passes validate() (counts are clipped into the budgets)."""
    assert len(x) == len(SPACE), f"expected {len(SPACE)} coordinates"
    v = {name: lo + (hi - lo) * min(1.0, max(0.0, float(u))) for (name, lo, hi), u in zip(SPACE, x)}
    per_leg = []
    for leg in range(RINGS_PER_LAP):
        bias = v["leg_bias"] if leg % 2 else -v["leg_bias"]
        per_leg.append(max(0, int(round(v["density"] * (1.0 + bias)))))
    sites = sum(per_leg)
    shares = {"Spire": v["spire_share"], "Cacti": v["cacti_share"], "Borromean": v["borromean_share"]}
    total_share = sum(shares.values())
    budget_plants = min(MAX_PLANTS, int(sites * min(v["fill"], SITE_MARGIN)))
    plants = []
    if sites and total_share > 0 and budget_plants:
        for sp, w in shares.items():
            n = int(round(budget_plants * w / total_share))
            if n:
                plants.append((sp, n))
        while plants and (sum(n for _, n in plants) > budget_plants or forest_prisms(tuple(plants)) > MAX_FOREST_PRISMS):
            sp, n = max(plants, key=lambda p: PLANT_PRISMS[p[0]] * p[1])
            plants = [(s, k - 1 if s == sp else k) for s, k in plants]
            plants = [(s, k) for s, k in plants if k > 0]
    creatures = int(round(v["creatures"])) if plants else 0
    return Lifeforms(
        plants=tuple(plants),
        creatures=(("Tadpole", min(MAX_CREATURES, creatures)),) if creatures else (),
        sites_per_leg=tuple(per_leg) if plants else (),
        offset_min=round(v["offset_min"], 1),
        offset_max=round(v["offset_min"] + v["offset_depth"], 1),
        side_deg=round(v["side_deg"], 1) % 360.0,
        side_spread_deg=round(v["side_spread_deg"], 1),
        fauna_corridor=(round(v["fauna_corridor"], 1)
                        if creatures and v["fauna_corridor"] >= LANE_OFFSET + MIN_RAIL_CLEARANCE else 0.0),
    )


def descriptors(s: Lifeforms) -> dict:
    """What a quality-diversity archive can bin a lifeform course by. Not scores: the model cannot see any of it."""
    return {
        "plants_per_leg": s.plant_count / RINGS_PER_LAP,
        "forest_prisms": forest_prisms(s.plants),
        "mean_offset": (s.offset_min + s.offset_max) / 2 if s.plants else 0.0,
        "all_round": s.side_spread_deg / 180.0 if s.plants else 0.0,
        "species": len(s.plants),
        "creatures": s.creature_count,
        "creatures_cross_line": bool(s.creatures) and s.fauna_corridor <= 0,
    }


# ── Measurement: what one plant weighs, READ from the shipped assets (Tollway's method) ──────────────────────
def _read(rel: str) -> str:
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


def _leaf_volume(text: str):
    """The authored leaf volume, or None. A zero vector is FloraVariantTuning's "keep" sentinel, not a leaf."""
    m = re.search(r"[Ll]eafSize: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+)\}", text)
    if not m:
        return None
    x, y, z = (float(g) for g in m.groups())
    return x * y * z if x * y * z > 0 else None


def _price(species: str):
    """(mean prisms, mean volume) of one grown plant of this species as the copied config grows it: the config's
    budget override when it imposes one, else each element's own budget; the element's leaf, else the prefab's.
    Priced per element and averaged, because SpreadElements rolls the four uniformly."""
    cfg = _read(FLORA_SOURCES[species])
    prefab = _read(f"{FLORA_PREFAB_DIR}/{species}Flora.prefab")
    prefab_leaf = _leaf_volume(prefab)
    pb = re.search(r"^  maxTotalSpawnedObjects: (\d+)", prefab, re.M)
    ov = re.search(r"^  MaxTotalSpawnedObjectsOverride: (-?\d+)", cfg, re.M)
    override = int(ov.group(1)) if ov and int(ov.group(1)) > 0 else None
    budgets, volumes = [], []
    for e in ELEMENTS:
        el = _read(f"{LIFEFORM_DIR}/{species} Flora {e}.asset")
        leaf = _leaf_volume(el)
        leaf = leaf if leaf is not None else prefab_leaf
        b = re.search(r"^    MaxTotalSpawnedObjects: (-?\d+)", el, re.M)
        own = int(b.group(1)) if b and int(b.group(1)) > 0 else (int(pb.group(1)) if pb else None)
        budget = override if override is not None else own
        assert leaf and budget, f"{species} {e}: no leaf or no budget anywhere - its forest would price at 0"
        budgets.append(budget)
        volumes.append(budget * leaf)
    return sum(budgets) / len(budgets), sum(volumes) / len(volumes)


PLANT_PRISMS, PLANT_VOLUME = {}, {}
for _sp in FLORA_SOURCES:
    PLANT_PRISMS[_sp], PLANT_VOLUME[_sp] = _price(_sp)


def forest_prisms(plants) -> int:
    return int(round(sum(PLANT_PRISMS[sp] * n for sp, n in plants)))


def forest_volume(plants) -> int:
    return int(round(sum(PLANT_VOLUME[sp] * n for sp, n in plants)))


def phase_band(s: Lifeforms) -> "tuple[int, int]":
    """(prisms, volume) the standing forest adds to the cell's phase ladder. (0, 0) for NONE."""
    return forest_prisms(s.plants), forest_volume(s.plants)


# ── Validation ───────────────────────────────────────────────────────────────
def validate(s: Lifeforms) -> "list[str]":
    """Everything about a setting that is arithmetic rather than taste."""
    e = []
    if s.is_none:
        return e
    for sp, n in s.plants:
        if sp not in FLORA_SOURCES:
            e.append(f"plant species {sp!r} is not in the catalogue {sorted(FLORA_SOURCES)}")
        if n <= 0:
            e.append(f"{sp}: a count of {n} plants")
    for cr, n in s.creatures:
        if cr not in FAUNA_SOURCES:
            e.append(f"creature {cr!r} is not in the catalogue {sorted(FAUNA_SOURCES)}")
        if n <= 0:
            e.append(f"{cr}: a count of {n} creatures")
    if len({sp for sp, _ in s.plants}) != len(s.plants) or len({c for c, _ in s.creatures}) != len(s.creatures):
        e.append("a species is listed twice")
    if len(s.sites_per_leg) > RINGS_PER_LAP:
        e.append(f"{len(s.sites_per_leg)} legs of sites on a {RINGS_PER_LAP}-ring circuit")
    if any(n < 0 for n in s.sites_per_leg):
        e.append("a negative site count")
    if s.site_count and not s.plants:
        e.append("sites with no plant species - prepared ground nothing grows on")
    if s.creatures and not s.plants:
        e.append("creatures with no plants: super-shielded rails are never food, so they would starve")
    if s.plant_count > MAX_PLANTS:
        e.append(f"{s.plant_count} plants (> {MAX_PLANTS})")
    known = tuple((sp, n) for sp, n in s.plants if sp in FLORA_SOURCES)
    if known and forest_prisms(known) > MAX_FOREST_PRISMS:
        e.append(f"the forest grows {forest_prisms(known)} prisms (> {MAX_FOREST_PRISMS})")
    if s.creature_count > MAX_CREATURES:
        e.append(f"{s.creature_count} creatures (> {MAX_CREATURES})")
    if s.offset_min < LANE_OFFSET + MIN_RAIL_CLEARANCE:
        e.append(f"offset_min {s.offset_min} u is inside the rails' clearance ({LANE_OFFSET + MIN_RAIL_CLEARANCE} u) - "
                 f"the C# would lift it, so the setting does not say what is sown")
    if s.offset_max < s.offset_min or s.offset_max > MAX_OFFSET:
        e.append(f"offset_max {s.offset_max} u outside [{s.offset_min}, {MAX_OFFSET}]")
    if not (0.0 <= s.side_deg <= 360.0) or not (0.0 <= s.side_spread_deg <= 180.0):
        e.append("side window out of range")
    w0, w1 = s.leg_window
    if not (0.0 <= w0 < w1 <= 1.0):
        e.append(f"leg window {s.leg_window} is not an ordered sub-range of [0, 1]")
    # The mouth guard lives in the C# (RegattaLifeformSites.Admissible skips a site inside a ring's mouth plus
    # ring_clearance, deterministically on every peer), because only the real circuit knows where its rings sit.
    # What is arithmetic here is the margin that makes a skip harmless: plants never need more than
    # SITE_MARGIN of the sites asked for, and RegattaLifeformSitesTests proves every preset keeps at least that
    # share on every shipped circuit.
    if s.plants and s.plant_count > SITE_MARGIN * s.site_count:
        e.append(f"{s.plant_count} plants need more than {SITE_MARGIN:.0%} of the {s.site_count} sites asked for - "
                 f"a site skipped at a ring mouth would leave two plants on one root")
    if s.ring_clearance < 0:
        e.append("a negative ring clearance")
    if s.fauna_corridor < 0 or (s.fauna_corridor > 0 and not s.creatures):
        e.append("a fauna corridor with no creatures (or a negative one)")
    if s.creatures and 0 < s.fauna_corridor < LANE_OFFSET + MIN_RAIL_CLEARANCE:
        e.append(f"a {s.fauna_corridor} u corridor does not even cover the rails")
    return e


# ── Rendering: the YAML author_regatta_assets.py writes ──────────────────────
def _num(v) -> str:
    v = float(v)
    return str(int(v)) if v.is_integer() else repr(round(v, 4))


def _int_array(values) -> str:
    """A Unity primitive int array: an empty one is [], otherwise little-endian int32 hex."""
    if not values:
        return "[]"
    return "".join(int(v).to_bytes(4, "little", signed=True).hex() for v in values)


def prefab_fields(s: Lifeforms) -> str:
    """The SpawnableRegattaRails.lifeforms block, in field order (RegattaLifeformSiteSettings)."""
    return ("  lifeforms:\n"
            f"    sitesPerLeg: {_int_array(s.sites_per_leg)}\n"
            f"    offsetMin: {_num(s.offset_min)}\n"
            f"    offsetMax: {_num(s.offset_max)}\n"
            f"    sideDegrees: {_num(s.side_deg)}\n"
            f"    sideSpreadDegrees: {_num(s.side_spread_deg)}\n"
            f"    legWindowStart: {_num(s.leg_window[0])}\n"
            f"    legWindowEnd: {_num(s.leg_window[1])}\n"
            f"    ringClearance: {_num(s.ring_clearance)}\n"
            f"    faunaCorridorRadius: {_num(s.fauna_corridor)}\n"
            f"    faunaCorridorStandoff: {_num(s.fauna_standoff)}\n")


def _set_fields(text: str, src: str, pairs) -> str:
    for key, value in pairs:
        text, n = re.subn(rf"^  {key}: -?[0-9.]+$", f"  {key}: {value}", text, flags=re.M)
        assert n == 1, f"{src}: {key} not found once"
    return text


def _renamed(text: str, name: str) -> str:
    return re.sub(r"^  m_Name: .*$", f"  m_Name: {name}", text, count=1, flags=re.M)


def _listing(key: str, guids) -> str:
    if not guids:
        return f"  {key}: []\n"
    return f"  {key}:\n" + "".join(f"  - {{fileID: 11400000, guid: {x}, type: 2}}\n" for x in guids)


def own_assets(entry: str, s: Lifeforms, guid, header_for, spawn_profile_script: str):
    """The species configs and the spawn profile one course needs: [(rel, guid, content)], profile guid.
    `entry` names the course ("1".."4" today, a pool entry tomorrow). Nothing for NONE."""
    if s.is_none:
        return [], None
    out, flora, fauna = [], [], []
    for sp, n in s.plants:
        src = FLORA_SOURCES[sp]
        text = _set_fields(_read(src), src, (("InitialSpawnCount", n), ("PopulationSize", n), ("MaxLivePopulation", n),
                                             ("GrowthPerOffspring", 0)))
        assert "  NetworkSynced: 1\n" in text, f"{src} is not NetworkSynced - every peer would grow its own forest"
        name = f"Regatta {entry} {sp} Flora"
        flora.append(guid(f"asset/{name}"))
        out.append((f"{CELL_DIR}/{name}.asset", flora[-1], _renamed(text, name)))
    for cr, n in s.creatures:
        src = FAUNA_SOURCES[cr]
        text = _set_fields(_read(src), src, (("InitialSpawnCount", n), ("PopulationSize", n), ("MaxLivePopulation", n)))
        if "  NetworkSynced:" not in text:
            text, k = re.subn(r"^(  SpawnProbability: [0-9.]+\n)", r"\1  NetworkSynced: 1\n", text, count=1, flags=re.M)
            assert k == 1, f"{src}: SpawnProbability not found once"
        else:
            text = re.sub(r"^  NetworkSynced: \d$", "  NetworkSynced: 1", text, count=1, flags=re.M)
        name = f"Regatta {entry} {cr} Fauna Config Data"
        fauna.append(guid(f"asset/{name}"))
        out.append((f"{CELL_DIR}/{name}.asset", fauna[-1], _renamed(text, name)))

    pname = f"Regatta Spawn Profile {entry}"
    g_profile = guid(f"asset/{pname}")
    body = (header_for(spawn_profile_script, pname)
            + "  FloraExcludeLocalDomain: 0\n"
            + "  FloraSpawnVolumeCeiling: 12000\n"
            + "  FloraInitialDelaySeconds: 0\n"
            + "  FloraSpawnIntervalSeconds: 0.5\n"
            + "  FloraPopulationScale: 1\n"
            + "  FloraPlantBudgetScale: 1\n"
            + "  FloraPrismScale: 1\n"
            + _listing("SupportedFloras", flora)
            + "  FaunaExcludeLocalDomain: 0\n"
            + "  InitialFaunaSpawnWaitTime: 10\n"
            + "  InitialFaunaReleaseTier: 2147483647\n"
            + "  FaunaSpawnVolumeThreshold: 1\n"
            + "  FaunaPopulationScale: 1\n"
            + "  BaseFaunaSpawnTime: 30\n"
            + "  SeedFullWaveEveryTick: 0\n"
            + "  FaunaFoodFloor: 5\n"
            + "  FaunaInitialDelaySeconds: 0\n"
            + "  FaunaSpawnIntervalSeconds: 1\n"
            + "  HerbivoreSpawnPointCount: 0\n"
            + "  HerbivoreSpawnRadius: 400\n"
            + "  PredatorSpawnPointCount: 0\n"
            + "  PredatorSpawnRadius: 600\n"
            + _listing("SupportedFaunas", fauna))
    out.append((f"{CELL_DIR}/{pname}.asset", g_profile, body))
    return out, g_profile


# ── Self-test ────────────────────────────────────────────────────────────────
def _cs_const(rel: str, name: str) -> float:
    m = re.search(rf"{name}\s*=\s*([0-9.]+)f?;", _read(rel))
    assert m, f"{rel}: {name} not found"
    return float(m.group(1))


def _self_test() -> int:
    fails = []

    def expect(cond, what):
        if not cond:
            fails.append(what)

    cs = "Assets/_Scripts/Controller/Arcade/Regatta/RegattaLifeformSites.cs"
    expect(_cs_const(cs, "MinRailClearance") == MIN_RAIL_CLEARANCE, "MIN_RAIL_CLEARANCE drifted from the C#")
    # The C# defaults ARE NONE: a prefab authored from NONE serializes what a fresh component holds.
    src = _read(cs)
    for field, want in (("offsetMin", NONE.offset_min), ("offsetMax", NONE.offset_max), ("sideDegrees", NONE.side_deg),
                        ("sideSpreadDegrees", NONE.side_spread_deg), ("legWindowStart", NONE.leg_window[0]),
                        ("legWindowEnd", NONE.leg_window[1]), ("ringClearance", NONE.ring_clearance),
                        ("faunaCorridorRadius", NONE.fauna_corridor), ("faunaCorridorStandoff", NONE.fauna_standoff)):
        m = re.search(rf"public float {field} = ([0-9.]+)f;", src)
        expect(m and float(m.group(1)) == want, f"C# default {field} is not NONE's {want}")
    expect("sitesPerLeg: []\n" in prefab_fields(NONE) and "faunaCorridorRadius: 0\n" in prefab_fields(NONE),
           "NONE does not render an empty course")
    expect(_int_array([3, 1]) == "0300000001000000", "int array hex is not little-endian int32")
    expect(phase_band(NONE) == (0, 0), "NONE raises the phase ladder")

    def fake_guid(name):
        import hashlib
        return hashlib.md5(name.encode()).hexdigest()

    hdr = lambda script, name: f"HEADER {script} {name}\n"
    files, prof = own_assets("1", NONE, fake_guid, hdr, "s")
    expect(files == [] and prof is None, "NONE emits assets")

    for name, preset in PRESETS.items():
        expect(validate(preset) == [], f"preset {name} fails validation: {validate(preset)}")
        files, prof = own_assets("1", preset, fake_guid, hdr, "s")
        expect(prof is not None and len(files) == len(preset.plants) + len(preset.creatures) + 1,
               f"preset {name}: wrong asset count")
        for rel, _, text in files:
            if rel.endswith("Flora.asset"):
                expect("  NetworkSynced: 1\n" in text and "  GrowthPerOffspring: 0\n" in text,
                       f"{rel}: not networked, or it breeds off its site")
            if "Fauna Config" in rel:
                expect("  NetworkSynced: 1\n" in text, f"{rel}: creature not opted in to FaunaNetworkSync")
        band = phase_band(preset)
        expect(band[0] > 0 and band[1] > 0, f"preset {name}: its forest does not raise the ladder")

    # Every point the search can reach decodes into a valid setting (a corner sweep and a lattice).
    import itertools
    import random
    rng = random.Random(7)
    pts = [[rng.random() for _ in SPACE] for _ in range(400)]
    pts += [list(c) for c in itertools.islice(itertools.product((0.0, 1.0), repeat=len(SPACE)), 0, 4096, 37)]
    bad = [(p, validate(decode(p))) for p in pts if validate(decode(p))]
    expect(not bad, f"{len(bad)} decoded points fail validation, e.g. {bad[:1]}")
    expect(decode([0.0] * len(SPACE)).is_none or not decode([0.0] * len(SPACE)).plants,
           "the zero point of the space grows a forest")

    # Negative controls: each of these must be caught.
    base = PRESETS["thicket"]
    for what, broken in (
        ("more plants than sites", dataclasses.replace(base, sites_per_leg=(1,) * 8)),
        ("plants on the rails", dataclasses.replace(base, offset_min=30.0)),
        ("unknown species", dataclasses.replace(base, plants=(("Gyroid", 4),))),
        ("starving creatures", dataclasses.replace(NONE, creatures=(("Tadpole", 4),))),
        ("too many creatures", dataclasses.replace(PRESETS["reef"], creatures=(("Tadpole", 40),))),
        ("no spare sites", dataclasses.replace(base, sites_per_leg=(3,) * 8)),
        ("an unordered leg window", dataclasses.replace(base, leg_window=(0.8, 0.2))),
        ("an oversized forest", dataclasses.replace(base, plants=(("Borromean", 30),), sites_per_leg=(6,) * 8)),
        ("a corridor without creatures", dataclasses.replace(base, fauna_corridor=100.0)),
    ):
        expect(validate(broken) != [], f"negative control not caught: {what}")

    # A config that is NOT networked must be refused when rendered.
    real = FLORA_SOURCES["Spire"]
    try:
        FLORA_SOURCES["Spire"] = "Assets/_SO_Assets/Lifeforms/Spire Flora Space.asset"
        try:
            own_assets("1", PRESETS["groves"], fake_guid, hdr, "s")
            caught = "NetworkSynced: 1" in _read(FLORA_SOURCES["Spire"])
        except AssertionError:
            caught = True
        expect(caught, "an un-networked plant config was rendered")
    finally:
        FLORA_SOURCES["Spire"] = real

    if fails:
        print("regatta_lifeforms self-test FAILED:")
        for f in fails:
            print("  -", f)
        return 1
    print(f"regatta_lifeforms self-test OK: {len(PRESETS)} presets, {len(pts)} decoded points, 10 negative controls.")
    return 0


def describe() -> str:
    lines = ["Plant catalogue (one grown plant, read from the shipped assets):"]
    for sp in FLORA_SOURCES:
        lines.append(f"  {sp:10s} {PLANT_PRISMS[sp]:7.1f} prisms  {PLANT_VOLUME[sp]:9,.0f} volume")
    lines.append("Presets:")
    for name, p in PRESETS.items():
        d = descriptors(p)
        lines.append(f"  {name:12s} {p.plant_count:2d} plants on {p.site_count:2d} sites, {d['forest_prisms']:5d} prisms, "
                     f"offset {p.offset_min:.0f}-{p.offset_max:.0f} u, {p.creature_count} creatures"
                     + (f", corridor {p.fauna_corridor:.0f} u" if p.fauna_corridor else ""))
    lines.append("The offline race model sees none of this: every lifeform course is judged in the real game.")
    return "\n".join(lines)


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        sys.exit(_self_test())
    print(describe())
