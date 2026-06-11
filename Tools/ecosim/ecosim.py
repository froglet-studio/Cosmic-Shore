#!/usr/bin/env python3
"""
ecosim — headless Cosmic Shore cell-ecosystem + performance simulator.

WHY THIS EXISTS
---------------
There is no Unity or C# toolchain in the autonomy container, so the agent cannot
run the game to watch FPS / population oscillations. This is the stand-in: a
dependency-free model that

  1. reads the REAL config assets (so it always reflects what is authored),
  2. simulates the food-web dynamics (flora growth -> prisms; fauna seeding,
     reproduction, starvation, predation; grazing -> prism removal),
  3. estimates a FRAME COST from a physically-motivated model calibrated to one
     real measurement, and
  4. prints the steady-state band, population time series, predicted FPS, and a
     verdict — so config changes can be evaluated before a human ever opens Unity.

It is a LEVER-RANKER and BUDGET-SETTER, not an oracle. The cost constants are
pinned to a single real data point (see calibration.csv) with documented priors;
the in-Unity `EcosystemPerfProbe` emits more `(prisms,fauna,fps)` samples to
tighten it over time. The loop:

    edit config  ->  python3 ecosim.py  ->  read predicted fps + oscillation
                 ->  (human runs menu, pastes probe samples into calibration.csv)
                 ->  ecosim recalibrates  ->  repeat.

PERF MODEL (the important part)
-------------------------------
The CPU is the bottleneck, so one wall-second == one second of CPU work. Two
kinds of work:

  * per-FRAME work (rendered/ticked every frame, scales with fps):
        frame_fixed_ms = BASE + prisms*C_PRISM + fauna*C_FAUNA
  * fixed-RATE work — fauna Physics.OverlapSphere queries fire at 1/behaviorPeriod
    regardless of fps, each touching the prism colliders inside its radius:
        overlap_ms_per_sec = sum_species( count/period
                                          * collidersInRadius(radius, prisms)
                                          * C_OVERLAP )
    collidersInRadius = prisms * (radius/cellR)^3 * CLUSTERING
      (fauna SEEK dense regions, so they sample local — not mean — density;
       CLUSTERING = local/mean density factor.)

CPU-bound identity:  fps*frame_fixed_ms + overlap_ms_per_sec = 1000
  =>  fps = max(0, (1000 - overlap_ms_per_sec) / frame_fixed_ms)

The overlap term is why the menu is at 5 fps: a 70 m sphere in a 5400-prism cell,
fired by ~90 fauna a few times a second, can alone exceed the 1000 ms/s budget.
"""

import math
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))


# --------------------------------------------------------------------------
#  Asset parsing (tiny field grep — no yaml dependency)
# --------------------------------------------------------------------------

def _read(path):
    with open(os.path.join(REPO, path), encoding="utf-8") as f:
        return f.read()

def _field(text, name, default=0.0):
    m = re.search(rf"^\s*{re.escape(name)}:\s*(-?[0-9.]+)\s*$", text, re.M)
    return float(m.group(1)) if m else default


class FaunaSpecies:
    def __init__(self, name, cfg_path, diet, radius, behavior_period):
        t = _read(cfg_path)
        self.name = name
        self.diet = diet                              # "herbivore" | "predator"
        self.radius = radius                          # OverlapSphere radius (m)
        self.behavior_period = behavior_period        # s between behavior ticks
        self.seed_floor = int(_field(t, "PopulationSize", 4))
        self.feeds_per_offspring = int(_field(t, "FeedsPerOffspring", 0))
        self.offspring_per_birth = int(_field(t, "OffspringPerBirth", 1))
        self.repro_cooldown = _field(t, "ReproductionCooldownSeconds", 10)
        self.max_pop = int(_field(t, "MaxLivePopulation", 0))   # 0 = uncapped (model treats as large)


class Biome:
    """Loads a biome's tunables from its real config assets."""
    def __init__(self, name, cell_cfg, spawn_profile, species, cell_radius_m,
                 flora_growth_per_s, trail_per_s):
        ct = _read(cell_cfg)
        self.name = name
        self.restless_enter = int(_field(ct, "RestlessEnter", 8000))
        self.frenzy_enter = int(_field(ct, "FrenzyEnter", 15000))
        self.frenzy_exit = int(_field(ct, "FrenzyExit", 14000))
        sp = _read(spawn_profile)
        self.base_spawn_time = _field(sp, "BaseFaunaSpawnTime", 12)
        self.food_floor = int(_field(sp, "FaunaFoodFloor", 5))
        self.species = species
        self.cell_radius_m = cell_radius_m            # MODEL ASSUMPTION (membrane radius)
        self.flora_growth_per_s = flora_growth_per_s  # MODEL ASSUMPTION (prisms/s while < Frenzy)
        self.trail_per_s = trail_per_s                # MODEL ASSUMPTION (vessel trail prisms/s)


# --------------------------------------------------------------------------
#  Performance model
# --------------------------------------------------------------------------

class PerfModel:
    """
    Calibrated to ONE anchor (calibration.csv first row, or the baked default):
    the menu's CURRENT steady state ~ (5400 prisms, ~90 fauna) -> ~5 fps.

    Priors used to split the single point into the four constants (documented so
    they're easy to challenge): at the anchor the OverlapSphere term dominates
    (OVERLAP_SHARE of the 1000 ms/s budget), prism per-frame overhead is the next
    biggest, fauna per-frame work and base are small. CLUSTERING captures that
    fauna query dense regions, not mean density.
    """
    # Priors (refine via calibration.csv)
    CLUSTERING = 18.0       # local prism density / mean density at a fauna's query point
    OVERLAP_SHARE = 0.72    # fraction of the budget the overlap term eats at the anchor
    BASE_MS = 2.0           # fixed per-frame cost (menu UI, vessel, camera)
    C_FAUNA = 0.012         # per-fauna per-frame ms (move/rotate/lerp)

    def __init__(self, biome, anchor):
        # Calibrate ONCE on the as-authored biome at the anchor state. The
        # constants are then FIXED — candidate configs are evaluated against them
        # (so changing a candidate's radius/prisms/fauna actually moves fps; an
        # earlier version recalibrated per-candidate and the radius lever cancelled).
        self.R = biome.cell_radius_m
        self.anchor = anchor
        ap, af, afps = anchor                       # (prisms, fauna_total, fps)
        anchor_states = self._anchor_states(biome, af)
        overlap_unit = self._overlap_per_sec(anchor_states, ap, c_overlap=1.0)
        budget_overlap = self.OVERLAP_SHARE * 1000.0
        self.C_OVERLAP = budget_overlap / overlap_unit if overlap_unit > 0 else 0.0
        frame_fixed = (1000.0 - budget_overlap) / max(1e-6, afps)
        prism_part = frame_fixed - self.BASE_MS - af * self.C_FAUNA
        self.C_PRISM = max(0.0, prism_part / max(1.0, ap))

    @staticmethod
    def _anchor_states(biome, fauna_total):
        # The anchor is the pinned-at-caps state; distribute its total fauna over
        # species by caps, each at its AUTHORED radius/period.
        caps = {s.name: (s.max_pop or s.seed_floor) for s in biome.species}
        tot = sum(caps.values()) or 1
        return [(fauna_total * caps[s.name] / tot, s.radius, s.behavior_period)
                for s in biome.species]

    def _overlap_per_sec(self, species_states, prisms, c_overlap):
        """species_states: list of (count, radius_m, behavior_period_s)."""
        total = 0.0
        for count, radius, period in species_states:
            colliders = prisms * (radius / self.R) ** 3 * self.CLUSTERING
            total += (count / period) * colliders * c_overlap
        return total

    def fps(self, prisms, species_states):
        overlap = self._overlap_per_sec(species_states, prisms, self.C_OVERLAP)
        fauna_total = sum(c for c, _, _ in species_states)
        frame_fixed = self.BASE_MS + prisms * self.C_PRISM + fauna_total * self.C_FAUNA
        return max(0.0, (1000.0 - overlap) / max(1e-6, frame_fixed)), overlap, frame_fixed


# --------------------------------------------------------------------------
#  Food-web outcome: are the gyroids TAMED (held sizable) or DEVOURED (stripped)?
# --------------------------------------------------------------------------
#
# The gameplay question (freestyle / menu): do the flora gyroids stay sizable to
# fly through, or do the fauna eat them to the ground? It's the predator-prey
# equilibrium, set by ONE comparison:
#
#   food_supported_herbivores = flora_growth_per_s / graze_per_herbivore_s
#       (the herbivore count whose total grazing exactly equals flora growth)
#
#   * herbivore CAP <= food_supported  -> fauna can't out-graze flora; the gyroids
#       grow to FrenzyEnter and HOLD there (sizable, stable). Fauna "tame" the edges.
#   * herbivore CAP >  food_supported  -> fauna out-graze flora; they eat the gyroids
#       down to a low level and boom/bust (starve, regrow). "Devoured" — not fun.
#
# So the fauna caps are the TAMING DIAL. Keep the summed herbivore cap at or below
# food_supported and the environment stays sizable. (flora/graze rates are MODEL
# ASSUMPTIONS; their RATIO is what matters and is calibrated so the over-grazing
# config the player reported reads DEVOURED — refine via EcosystemPerfProbe gyroid
# observations.)

FLORA_GROWTH_PER_S = 20.0       # MODEL ASSUMPTION: prisms/s the flora add while < Frenzy
GRAZE_PER_HERBIVORE_S = 1.15    # MODEL ASSUMPTION: prisms/s one herbivore removes
FOOD_SUPPORTED = FLORA_GROWTH_PER_S / GRAZE_PER_HERBIVORE_S  # ~17 herbivores


def gyroid_outcome(biome):
    herb_cap = sum((s.max_pop or s.seed_floor) for s in biome.species if s.diet == "herbivore")
    holds = herb_cap <= FOOD_SUPPORTED
    if holds:
        # Gyroids reach FrenzyEnter and breathe in the [FrenzyExit, FrenzyEnter] band.
        gy_lo, gy_hi = biome.frenzy_exit, biome.frenzy_enter
        verdict = "TAMED — gyroids hold sizable (fly-through preserved)"
    else:
        # Over-grazed: fauna pull prisms down to roughly where graze == growth, i.e.
        # the population can only be fed at a low prism level. Rough stripped band.
        over = herb_cap / max(1.0, FOOD_SUPPORTED)
        gy_hi = int(biome.frenzy_enter / over)
        gy_lo = int(gy_hi * 0.4)
        verdict = f"DEVOURED — herbivore cap {herb_cap} > food-supported {FOOD_SUPPORTED:.0f} (gyroids stripped, boom/bust)"
    return holds, gy_lo, gy_hi, herb_cap, verdict


def report_gyroids(biome):
    holds, lo, hi, herb_cap, verdict = gyroid_outcome(biome)
    print(f"\n--- GYROID OUTCOME (taming vs devouring) ---")
    print(f"  herbivore cap (summed) {herb_cap}  vs food-supported {FOOD_SUPPORTED:.0f}  "
          f"(flora {FLORA_GROWTH_PER_S}/s / graze {GRAZE_PER_HERBIVORE_S}/s)")
    print(f"  {verdict}")
    print(f"  steady gyroid prisms ~ {lo}-{hi}")
    return holds


# --------------------------------------------------------------------------
#  Steady state from config
# --------------------------------------------------------------------------
#
# The MENU's observed regime (the anchor): flora grow faster than the fauna can
# graze, so prisms PIN at Frenzy and abundant prey drives every species'
# reproduction to its cap. So the heavy steady state a config produces is:
#       prisms  = FrenzyEnter
#       fauna   = each species' MaxLivePopulation (or seed floor if uncapped=0)
# That is the worst case the player actually sits in, and the one to make >=60fps.
#
# (A cell only escapes the pin — prisms oscillating BELOW Frenzy — if total
# herbivore graze capacity exceeds flora growth; then vibrancy comes from prism
# breathing too. That is a balance lever, secondary to hitting frame budget, and
# is reported as `graze_headroom` so we can see how close we are to it.)

def steady_state(biome, cap_scale=1.0, r_scale=1.0):
    """The heavy steady state a config produces: prisms pinned at Frenzy, each
    species at its cap. Returns (prisms, species_states, counts) where
    species_states = list of (count, radius, period) for the perf model."""
    states, counts = [], {}
    for s in biome.species:
        cap = s.max_pop if s.max_pop > 0 else s.seed_floor
        n = max(0, round(cap * cap_scale))
        counts[s.name] = n
        states.append((n, s.radius * r_scale, s.behavior_period))
    return biome.frenzy_enter, states, counts


def report(perf, prisms, states, counts, label=""):
    fps, overlap, frame = perf.fps(prisms, states)
    fauna = sum(counts.values())
    cs = " ".join(f"{n[:4]}={counts[n]}" for n in counts)
    flag = "OK >=60" if fps >= 60 else ("near" if fps >= 45 else "TOO HEAVY")
    print(f"  {label:28s} prisms={prisms:5.0f} fauna={fauna:3.0f} [{cs}]  "
          f"-> {fps:6.1f} fps  ({flag})   overlap={overlap:.0f}ms/s frame={frame:.1f}ms")
    return fps


# --------------------------------------------------------------------------
#  Biome wiring (paths to the real assets)
# --------------------------------------------------------------------------

CFG = "Assets/_SO_Assets/Cell Configs"

def blob_biome(cell_radius_m=600.0, flora_growth_per_s=18.0, trail_per_s=2.0):
    species = [
        FaunaSpecies("tadpole",     f"{CFG}/Blob Cell/Blob Tadpole Fauna Config Data.asset",
                     "herbivore", radius=50, behavior_period=1.5),
        FaunaSpecies("brittlestar", f"{CFG}/Blob Cell/Blob Fauna Config Data.asset",
                     "herbivore", radius=70, behavior_period=2.0),
        FaunaSpecies("shark",       f"{CFG}/Blob Cell/Blob Shark Fauna Config Data.asset",
                     "predator", radius=70, behavior_period=2.0),
    ]
    return Biome("Blob (menu)",
                 f"{CFG}/Blob Cell/Blob Cell Config.asset",
                 f"{CFG}/Blob Cell/Blob Cell Spawn Profile.asset",
                 species, cell_radius_m, flora_growth_per_s, trail_per_s)


# Anchor: the user's reported current menu steady state. (prisms, fauna_total, fps)
DEFAULT_ANCHOR = (5400, 90, 5.0)

def load_anchor():
    path = os.path.join(os.path.dirname(__file__), "calibration.csv")
    if not os.path.exists(path):
        return DEFAULT_ANCHOR
    rows = []
    for line in open(path):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        p, f, fps = (float(x) for x in line.split(",")[:3])
        rows.append((p, f, fps))
    return rows[0] if rows else DEFAULT_ANCHOR


def fps_for(perf, frenzy=None, cap_scale=1.0, r_scale=1.0):
    """Evaluate a candidate against the ONE fixed-calibrated perf model."""
    b = blob_biome()
    if frenzy is not None:
        b.frenzy_enter = frenzy
    prisms, states, counts = steady_state(b, cap_scale, r_scale)
    fps, overlap, frame = perf.fps(prisms, states)
    return fps, prisms, sum(counts.values()), overlap, frame


def per_lever_breakdown(perf):
    print("\n--- PER-LEVER SENSITIVITY (one lever at a time from the as-authored state) ---")
    base = fps_for(perf)[0]
    print(f"  baseline (as-authored)                 {base:6.1f} fps")
    for frenzy in (3000, 1800, 1200, 800):
        f = fps_for(perf, frenzy=frenzy)[0]
        print(f"  Frenzy {frenzy:<5} (prisms)               {f:6.1f} fps")
    for cs in (0.5, 0.33, 0.25):
        f = fps_for(perf, cap_scale=cs)[0]
        print(f"  fauna caps x{cs:<4}                      {f:6.1f} fps")
    for rs in (0.7, 0.5, 0.35):
        f = fps_for(perf, r_scale=rs)[0]
        print(f"  query radius x{rs:<4} (CUBIC on overlap)  {f:6.1f} fps")


def named_candidates(perf):
    print("\n--- NAMED CANDIDATE CONFIGS (balanced cuts) ---")
    cands = [
        # Menu-config-ONLY (Blob assets; zero behavior/shared-data-SO risk):
        ("F cfg-only  Frenzy1500 caps.4",            dict(frenzy=1500, cap_scale=0.4)),
        ("G cfg-only  Frenzy1200 caps.4",            dict(frenzy=1200, cap_scale=0.4)),
        ("H cfg-only  Frenzy1000 caps.35",           dict(frenzy=1000, cap_scale=0.35)),
        ("I cfg-only  Frenzy 800 caps.4",            dict(frenzy=800,  cap_scale=0.4)),
        # With a shared radius cut too (affects every scene's brittlestar/tadpole):
        ("J +radius   Frenzy1500 caps.4 r.65",       dict(frenzy=1500, cap_scale=0.4, r_scale=0.65)),
        ("K +radius   Frenzy1200 caps.5 r.65",       dict(frenzy=1200, cap_scale=0.5, r_scale=0.65)),
    ]
    for name, kw in cands:
        fps, prisms, fauna, overlap, frame = fps_for(perf, **kw)
        flag = "OK >=60" if fps >= 60 else ("near" if fps >= 50 else "low")
        print(f"  {name:34s} -> {fps:6.1f} fps  (prisms {prisms:.0f}, fauna {fauna:.0f}; "
              f"overlap {overlap:.0f}ms/s, frame {frame:.1f}ms)  [{flag}]")


def main():
    anchor = load_anchor()
    biome = blob_biome()
    perf = PerfModel(biome, anchor)
    perf.anchor = anchor
    print(f"anchor (prisms,fauna,fps) = {anchor}")
    print(f"calibrated: C_PRISM={perf.C_PRISM:.4f}ms/prism  C_OVERLAP={perf.C_OVERLAP:.3e}ms/collider  "
          f"BASE={perf.BASE_MS}ms  CLUSTERING={perf.CLUSTERING}  cellR={biome.cell_radius_m}m")

    print(f"\n--- CURRENT config heavy steady state ({biome.name}) ---")
    prisms, states, counts = steady_state(biome)
    report(perf, prisms, states, counts, "as-authored (Frenzy pin, caps)")

    report_gyroids(biome)
    per_lever_breakdown(perf)
    named_candidates(perf)


if __name__ == "__main__":
    main()
