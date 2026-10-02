"""Hierarchical ecology (Direction E) - every tunable number in one place.

Units: world units (u) for distance, seconds for time, and VOLUME (the game's prism volume; a nominal prism
is 16) for mass. Every mass flow in the model is volume moving between ledger buckets (see world.Ledger), so
"conservation to the unit" means the ledger total is constant to float64 rounding.

Two species ride on one flora field:
  H  herbivore - grazes flora (and the skeletons starvation leaves behind)
  P  predator  - eats herbivores whole (body + stomach)
Each carries an ELEMENT label (charge/mass/space/time) that offspring inherit. Element is neutral in the
dynamics here (the population tracks it so the macro state can answer "how many Time grazers are in this
region"); the elemental laws (Time breeds faster etc.) are a parameter, not a structural change.
"""
from __future__ import annotations

from dataclasses import dataclass, field

ELEMENTS = ("charge", "mass", "space", "time")


@dataclass
class Species:
    name: str
    body: float          # body volume (the skeleton it leaves when it starves; what a predator eats)
    e0: float            # stomach an offspring is born with (paid by the parent)
    e_birth: float       # stomach at which an individual splits off one offspring (costs body + e0)
    e_max: float         # satiation scale: intake is scaled by (1 - e/e_max)
    metab: float         # vol/s burned to the nutrient pool, always (no clock: death is E <= 0)
    speed: float         # cruise speed
    sprint: float        # flee / chase speed
    sprint_metab: float  # extra vol/s while sprinting
    # phases are read off the stomach as a fraction of e_birth: sated > hi, hungry < lo, forage between
    phase_lo: float = 0.3
    phase_hi: float = 0.8


HERB = Species("herb", body=8.0, e0=6.0, e_birth=20.0, e_max=32.0, metab=0.04,
               speed=18.0, sprint=32.0, sprint_metab=0.04)
PRED = Species("pred", body=40.0, e0=30.0, e_birth=100.0, e_max=120.0, metab=0.04,
               speed=20.0, sprint=36.0, sprint_metab=0.06)


@dataclass
class Params:
    # ---- world ----
    R: float = 1200.0            # cell (membrane) radius
    L: float = 200.0             # region edge
    vox_per_axis: int = 4        # flora voxels per region axis (50 u voxels)
    # ---- flora (regional prism mass) ----
    flora_r: float = 0.006       # logistic growth rate /s
    flora_cap: float = 120.0     # volume a voxel can carry
    nutrient_half: float = 4000.0  # half-saturation of the region nutrient pool
    seed_rain: float = 0.02      # recolonisation: growth uses F + seed_rain * mean(region flora) (no flora from nothing)
    nutrient_diffuse: float = 0.002  # /s exchange of nutrient between face-neighbour regions (soil)
    # ---- herbivore grazing (Holling II on voxel flora) ----
    h_intake: float = 0.25       # max vol/s
    h_half: float = 90.0         # voxel grazeable volume at half intake
    occ_theta: float = 0.5       # macro occupancy relaxes toward food ** occ_theta ... (fitted, calibrate.fit_occupancy)
    occ_tau: float = 1.0         # ... with this time constant (s)
    # ---- predation (Holling II on region herbivore count; fitted to micro, see calibrate.py) ----
    p_attack: float = 0.0001     # kills / (predator * herbivore-in-region * s) at low density
    p_handle: float = 151.0        # s per kill (the chase)
    p_hunt_below: float = 0.8    # predators hunt only while stomach < this * e_max (satiation)
    # ---- micro predation geometry (what p_attack / p_handle are fitted FROM)
    p_sense: float = 25.0
    p_catch: float = 4.0
    h_flee: float = 40.0
    sprint_kappa: tuple = (0.36, 0.019)  # measured sprint time / Poisson-encounter odds (grazers flee, hunters chase)
    # ---- macro movement (regional hops; fitted to micro effective diffusion) ----
    hop_rate: dict = field(default_factory=lambda: {"herb": (0.00144, 0.00482, 0.0112), "pred": (0.0028, 0.0107, 0.0144)})
    # per-neighbour hop rate by phase (sated, forage, hungry)
    food_bias: float = 0.3       # hops weighted by (neighbour food / own food) ** food_bias for hungry herbivores
    prey_bias: float = 0.0       # same for predators on herbivore counts
    flee_bias: float = 0.0       # herbivores weight hops away from predators
    # ---- macro discretisation ----
    n_bins: int = 8              # (legacy macro_bins.py only)
    cohorts: int = 10            # cohort slots per region per species
    merge_tol: float = 0.06      # an arrival merges into a cohort whose mean stomach is within this x e_birth
    e_diffuse: tuple = (0.0009, 0.002)  # stomach diffusion within a cohort (vol^2/s), herb / pred (fitted)
    dt_macro: float = 1.0
    dt_micro: float = 0.1
    # ---- LOD ----
    expand_radius: float = 420.0     # region centre within this of a pilot -> hot
    expand_ahead: float = 700.0      # ... or within this AND inside the pilot's forward cone (prefetch)
    collapse_radius: float = 620.0   # hot region with no pilot within this -> cold (hysteresis band)
    visible_radius: float = 450.0    # an agent closer than this to a pilot, inside its cone, is SEEN
    cone_cos: float = 0.5            # forward cone half-angle 60 deg
    reps_per_region: int = 6         # impostor representatives per (region, species)
    bloom_s: float = 1.5             # emerging agents disperse from their representative over this
    seed: int = 7
    # ---- NEGATIVE CONTROLS: a planted bug the gates must catch (tests/). Never set in a real run.
    bug: str = ""
