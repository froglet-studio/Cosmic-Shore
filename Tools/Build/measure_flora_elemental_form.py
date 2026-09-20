#!/usr/bin/env python3
"""Measure and gate THE FOUR ELEMENTAL IDENTITIES OF A PLANT (Docs/ECOSYSTEM.md §45).

The law is one sentence per element:

    CHARGE armours its leaves.        (a state - Flora.ResolveShieldPeriod, §35)
    MASS   is the most cumulative prism volume, in the most CUBIC leaf.
    SPACE  is the highest ASPECT RATIO - its long axis trades that volume for the
           BOUNDING VOLUME of the assembly.
    TIME   is the fastest clock: it grows and reproduces fastest.

This tool exists because the two shape halves are CONSTANTS, and a constant nobody can
re-derive is a constant nobody can check. It does four separate jobs, and they answer four
different questions:

  1. MEASURE   - re-derives the shipped constants from the assets that already state the
                 law, so `FloraElementalForm`'s numbers are a measurement rather than a
                 taste. Three lattice species author four fitted leaves each; the eight
                 Hesperides phyllotactics share ONE authored cross-section ladder. Each
                 FAMILY gets one vote (eight species sharing one table is one decision, not
                 eight), and the law is the geometric mean of the two family medians.
  2. VERIFY    - compiles and RUNS the shipped C# (`Tools/Build/flora_form_harness/`) and
                 compares it against an independent transcription here. This is the step
                 neither the measurement nor code review can see.
  3. NEUTRALITY- proves the law is a REDISTRIBUTION and not an inflation: the four volume
                 multipliers average to exactly 1 and the anisotropy term is volume-exact,
                 so no cell's volume phase ladder moves (Docs/ECOSYSTEM.md §4.6).
  4. COMPLY    - the species EXEMPT from the runtime transform (a lattice, whose leaf is
                 fitted to its own bond table) must state the law in their own data
                 instead. Checks the direction on every one of them, and names any that
                 contradicts it.

    measure_flora_elemental_form.py              # report
    measure_flora_elemental_form.py --check      # fail the build
    measure_flora_elemental_form.py --self-test  # prove the gates can fail
"""
from __future__ import annotations

import argparse
import json
import pathlib
import re
import statistics
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
LIFEFORMS = ROOT / "Assets/_SO_Assets/Lifeforms"
LAW = ROOT / "Assets/_Scripts/Utility/DataContainers/FloraElementalForm.cs"
HARNESS = ROOT / "Tools/Build/flora_form_harness/run.sh"

ELEMENTS = ("Charge", "Mass", "Space", "Time")
ELEMENT_ID = {0: "None", 1: "Charge", 2: "Mass", 3: "Space", 4: "Time", 5: "Omni"}

# Families whose prism size is dictated by their growth rule (Flora.PrismSizeFixedByGrowthRule):
# EXEMPT from the runtime transform, and therefore CHECKED against the law instead.
EXEMPT_PREFABS = {"GyroidFlora", "SchwarzPFlora", "QuasicrystalFlora",
                  "MandelbulbFlora", "CoralBloomFlora", "WatershedFlora", "ApolloniaFlora"}
# The three that author a fitted per-element leaf and so can be measured FROM. The Mandelbulb
# states its per-element form in code rather than in a LeafSize, so it is checked, not
# measured (see `mandelbulb_flora_model.py`).
LATTICE_PREFABS = {"GyroidFlora", "SchwarzPFlora", "QuasicrystalFlora"}

# PrismScaleAnimator.SetTargetScale clamps per axis into this window INSIDE the setter, with
# no log and no return value (363 of 404 prefabs inherit the defaults). Flora.AddHealthBlock
# widens it with AdmitTargetScale; PhyllotacticFlora.AddHealthBlock deliberately does not
# (Docs/ECOSYSTEM.md §34.9), so a phyllotactic axis outside this window is silently trimmed.
PRISM_SCALE_MIN, PRISM_SCALE_MAX = 0.5, 10.0
NON_ADMITTING_PREFABS = {
    "ArborFlora", "CoralFlora", "FrondFlora", "LanternFlora",
    "ReedFlora", "RosetteFlora", "SpireFlora", "TendrilFlora",
}


# ── reading the shipped assets ──────────────────────────────────────────────────────────

def prefab_guids() -> dict[str, str]:
    out = {}
    for meta in ROOT.rglob("*.prefab.meta"):
        m = re.search(r"^guid: ([0-9a-f]{32})", meta.read_text(errors="ignore"), re.M)
        if m:
            out[m.group(1)] = meta.name[: -len(".prefab.meta")]
    return out


def read_flora_configs() -> list[dict]:
    guids = prefab_guids()
    rows = []
    for path in sorted(LIFEFORMS.glob("*.asset")):
        text = path.read_text(errors="ignore")
        if "FloraPrefab" not in text:
            continue
        g = re.search(r"FloraPrefab:\s*\{fileID: \d+, guid: ([0-9a-f]{32})", text)
        prefab = guids.get(g.group(1), "?") if g else "?"
        el = re.search(r"^  Element: (-?\d+)", text, re.M)
        element = ELEMENT_ID.get(int(el.group(1)), "?") if el else "?"

        leaf = None
        block = re.search(r"Variant:\s*\n((?:    .*\n)+)", text)
        if block:
            # A SENTINEL IS NOT A MEASUREMENT: LeafSize {0,0,0} means "keep the prefab's",
            # so it must be resolved the way the runtime resolves it - as absent - rather
            # than read as a real leaf. `-?` so -1 is skipped by rule, not by accident.
            ls = re.search(
                r"LeafSize:\s*\{x: (-?[\d.eE+-]+), y: (-?[\d.eE+-]+), z: (-?[\d.eE+-]+)\}",
                block.group(1))
            if ls:
                v = tuple(float(x) for x in ls.groups())
                if all(c > 0 for c in v):
                    leaf = v
        rows.append(dict(asset=path.stem, prefab=prefab, element=element, leaf=leaf))
    return rows


def by_species(rows) -> dict[str, dict[str, dict]]:
    out: dict[str, dict[str, dict]] = {}
    for r in rows:
        out.setdefault(r["prefab"], {})[r["element"]] = r
    return out


# ── the measurement: what each species SAYS the law is, against TIME as the neutral ─────

def log_shape(leaf):
    """A leaf's unit-volume SHAPE in log space (zero-sum), i.e. its aspect with size removed."""
    import math
    logs = [math.log(c) for c in leaf]
    mean = sum(logs) / 3.0
    return [x - mean for x in logs]


def measure_species(elements: dict[str, dict]) -> dict | None:
    """Per-element volume ratio and anisotropy exponent against this species' TIME leaf.

    TIME is the neutral form on purpose: its identity is the clock, so its leaf is the
    species' own. (The four original GyroidFlora variants confirm it - Charge and Time
    shipped the SAME 9x3.4x1.5 leaf, and Charge only diverged later when
    fit_shield_clearance.py re-fitted it for its armour, which is a consequence of the
    Charge law rather than a second identity.)
    """
    ref = elements.get("Time", {}).get("leaf")
    if not ref:
        return None
    ref_vol = ref[0] * ref[1] * ref[2]
    ref_shape = log_shape(ref)
    denom = sum(s * s for s in ref_shape)
    if denom <= 1e-9:
        return None  # a cubic reference has no aspect to compare against

    out = {}
    for element in ELEMENTS:
        leaf = elements.get(element, {}).get("leaf")
        if not leaf:
            continue
        vol = leaf[0] * leaf[1] * leaf[2]
        shape = log_shape(leaf)
        out[element] = dict(
            leaf=leaf,
            volume=vol,
            volume_ratio=vol / ref_vol,
            aspect=max(leaf) / min(leaf),
            # Least-squares exponent: the power the reference shape must be raised to in
            # order to land on this element's shape.
            anisotropy=sum(a * b for a, b in zip(shape, ref_shape)) / denom,
        )
    return out


def geo_mean(values):
    import math
    return math.exp(sum(math.log(v) for v in values) / len(values))


# ── the independent transcription of the transform ──────────────────────────────────────

def py_shape_leaf(leaf, volume, anisotropy):
    if min(leaf) <= 0:
        return leaf
    mean = (leaf[0] * leaf[1] * leaf[2]) ** (1.0 / 3.0)
    size = mean * volume ** (1.0 / 3.0)
    return tuple(size * (c / mean) ** anisotropy for c in leaf)


def shipped_constants() -> dict[str, float]:
    text = LAW.read_text()
    out = {}
    for name in ("MassLeafVolume", "SpaceLeafVolume", "NeutralLeafVolume",
                 "MassLeafAnisotropy", "SpaceLeafAnisotropy"):
        m = re.search(rf"public const float {name} = ([\d.]+)f;", text)
        if not m:
            raise SystemExit(f"FloraElementalForm.{name} not found - the law moved")
        out[name] = float(m.group(1))
    return out


def run_harness(*args) -> object:
    proc = subprocess.run([str(HARNESS), *args], capture_output=True, text=True)
    if proc.returncode != 0:
        raise SystemExit(f"harness failed ({proc.returncode}):\n{proc.stderr}")
    if not proc.stdout.strip():
        # An EMPTY result is a harness failure with its own message, never something to
        # hand to the parser - `Expecting value: line 1 column 1` names nothing.
        raise SystemExit(f"harness produced no output:\n{proc.stderr}")
    return json.loads(proc.stdout)


# ── the four jobs ───────────────────────────────────────────────────────────────────────

def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="fail the build on any violation")
    ap.add_argument("--self-test", action="store_true", help="prove each gate can fail")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    rows = read_flora_configs()
    species = by_species(rows)
    consts = shipped_constants()
    failures: list[str] = []

    # ── 1. MEASURE ──────────────────────────────────────────────────────────────────────
    print("=" * 78)
    print("1. WHAT THE SHIPPED ASSETS SAY THE LAW IS (per-element, against TIME)")
    print("=" * 78)
    lattice_vol = {e: [] for e in ("Mass", "Space")}
    lattice_ani = {e: [] for e in ("Mass", "Space")}
    phyllo_vol = {e: [] for e in ("Mass", "Space")}

    for name in sorted(species):
        m = measure_species(species[name])
        if not m:
            continue
        tag = "LATTICE " if name in LATTICE_PREFABS else \
              "exempt  " if name in EXEMPT_PREFABS else "runtime "
        print(f"\n  {tag}{name}")
        for element in ELEMENTS:
            e = m.get(element)
            if not e:
                continue
            print(f"    {element:7s} leaf={tuple(round(c,3) for c in e['leaf'])!s:26s} "
                  f"vol={e['volume']:8.3f} (x{e['volume_ratio']:5.3f})  "
                  f"aspect={e['aspect']:6.2f}  A={e['anisotropy']:+.3f}")
            if element in ("Mass", "Space"):
                if name in LATTICE_PREFABS:
                    lattice_vol[element].append(e["volume_ratio"])
                    lattice_ani[element].append(e["anisotropy"])
                elif name not in EXEMPT_PREFABS:
                    phyllo_vol[element].append(e["volume_ratio"])

    print("\n" + "-" * 78)
    print("  DERIVED (one vote per FAMILY - the eight phyllotactics share one authored")
    print("  table, so they are one decision; geometric mean of the two family medians):")
    derived = {}
    for element in ("Mass", "Space"):
        lat = statistics.median(lattice_vol[element])
        phy = statistics.median(phyllo_vol[element]) if phyllo_vol[element] else None
        raw = geo_mean([lat, phy]) if phy else lat
        derived[f"{element}Volume"] = raw
        derived[f"{element}Anisotropy"] = statistics.median(lattice_ani[element])
        print(f"    {element:6s} volume: lattice median {lat:6.3f}, phyllotactic median "
              f"{phy if phy is None else round(phy,3)} -> raw x{raw:6.3f}")
        print(f"    {element:6s} aspect: lattice median A={derived[f'{element}Anisotropy']:+.3f} "
              f"(phyllotactic authors a SQUARE cross-section, so it has no opinion)")

    # Normalise volume so the four average to exactly 1 - the neutrality property.
    raw = [1.0, derived["MassVolume"], derived["SpaceVolume"], 1.0]
    norm = sum(raw) / 4.0
    expect = dict(
        NeutralLeafVolume=1.0 / norm,
        MassLeafVolume=derived["MassVolume"] / norm,
        SpaceLeafVolume=derived["SpaceVolume"] / norm,
        MassLeafAnisotropy=derived["MassAnisotropy"],
        SpaceLeafAnisotropy=derived["SpaceAnisotropy"],
    )
    print(f"\n    normalised by x{1/norm:.4f} so the four average to exactly 1")
    print("\n    constant                shipped     re-derived   drift")
    for key, want in expect.items():
        got = consts[key]
        drift = abs(got - want) / max(want, 1e-9)
        # 2% on the volumes and 8% on the anisotropy exponents: the underlying spread is
        # three independent authoring decisions, so the gate catches a constant that stopped
        # tracking the assets, never the last digit of a median.
        tol = 0.08 if "Anisotropy" in key else 0.02
        flag = "OK" if drift <= tol else "DRIFT"
        if drift > tol:
            failures.append(
                f"FloraElementalForm.{key} = {got} but the shipped assets re-derive "
                f"{want:.4f} ({drift*100:.1f}% off, tolerance {tol*100:.0f}%)")
        print(f"    {key:24s}{got:9.4f}{want:13.4f}   {drift*100:5.1f}%  {flag}")

    # ── 2. VERIFY: the shipped C#, compiled and RUN ─────────────────────────────────────
    print("\n" + "=" * 78)
    print("2. THE SHIPPED C#, COMPILED AND RUN, against an independent transcription")
    print("=" * 78)
    probes = ["9,3.4,1.5", "3.51,3.51,2", "4,4,1", "1.3,1.3,2", "15,3,3", "0.85,0.85,0.85"]
    table = run_harness("table")
    shaped = run_harness("shape", *probes)
    worst = 0.0
    for row in shaped:
        leaf = tuple(row["authored"])
        for element in ELEMENTS:
            want = py_shape_leaf(leaf,
                                 table[element]["volume"], table[element]["anisotropy"])
            got = tuple(row[element])
            for a, b in zip(want, got):
                worst = max(worst, abs(a - b) / max(abs(a), 1e-9))
    print(f"    {len(probes)} leaves x 4 elements: worst relative disagreement "
          f"{worst:.3e}")
    if worst > 1e-5:
        failures.append(f"shipped C# disagrees with the model by {worst:.3e} (float32 "
                        f"rounding alone is under 1e-5)")

    # ── 3. NEUTRALITY ──────────────────────────────────────────────────────────────────
    print("\n" + "=" * 78)
    print("3. THE LAW IS A REDISTRIBUTION, NOT AN INFLATION")
    print("=" * 78)
    mean_volume = sum(table[e]["volume"] for e in ELEMENTS) / 4.0
    print(f"    mean per-leaf volume multiplier over the four elements: {mean_volume:.6f}")
    if abs(mean_volume - 1.0) > 2e-4:
        failures.append(f"the four leaf volume multipliers average {mean_volume:.6f}, not 1 - "
                        f"every cell growing a mixed-element forest needs its volume phase "
                        f"ladder re-derived (Docs/ECOSYSTEM.md §4.6)")
    else:
        print("    -> a mixed-element forest holds the mass it held before this law existed,")
        print("       so no cell's volume phase ladder moves.")

    # The anisotropy term must be volume-EXACT or volume and aspect are not independent.
    ani_worst = 0.0
    for row in shaped:
        leaf = tuple(row["authored"])
        base = leaf[0] * leaf[1] * leaf[2]
        for element in ELEMENTS:
            got = tuple(row[element])
            ratio = (got[0] * got[1] * got[2]) / base
            ani_worst = max(ani_worst, abs(ratio - table[element]["volume"])
                            / table[element]["volume"])
    print(f"    anisotropy's effect on volume: worst {ani_worst:.3e} (must be ~0 - aspect "
          f"and volume are independent dials)")
    if ani_worst > 1e-4:
        failures.append(f"the anisotropy term moves volume by {ani_worst:.3e}; it must be "
                        f"volume-exact or the neutrality proof above is void")

    # ── 4. COMPLY: the exempt species state the law in their own data ──────────────────
    print("\n" + "=" * 78)
    print("4. THE EXEMPT SPECIES MUST STATE THE LAW THEMSELVES")
    print("=" * 78)
    print("    (a lattice bonds at offsets in absolute units, so the runtime transform")
    print("     cannot touch it - Flora.PrismSizeFixedByGrowthRule, Docs/ECOSYSTEM.md §34.8)")
    for name in sorted(EXEMPT_PREFABS):
        m = measure_species(species.get(name, {}))
        if not m or not all(e in m for e in ELEMENTS):
            print(f"    {name:20s} states its form in CODE, not in a LeafSize - checked by "
                  f"measure_mandelbulb_flora.py --check, which gates all four clauses on "
                  f"the measured PLANT")
            continue
        vols = {e: m[e]["volume"] for e in ELEMENTS}
        asps = {e: m[e]["aspect"] for e in ELEMENTS}
        ok = []
        if max(vols, key=vols.get) == "Mass":
            ok.append("Mass heaviest")
        else:
            failures.append(f"{name}: MASS is not its heaviest leaf "
                            f"({max(vols, key=vols.get)} is) - the Mass law says it must be")
        if vols["Space"] < vols["Time"]:
            ok.append("Space lighter than Time")
        else:
            failures.append(f"{name}: SPACE's leaf ({vols['Space']:.2f}) is not lighter than "
                            f"TIME's ({vols['Time']:.2f}) - Space trades volume for reach")
        if max(asps, key=asps.get) == "Space":
            ok.append("Space most elongated")
        else:
            failures.append(f"{name}: SPACE does not have the highest aspect ratio "
                            f"({max(asps, key=asps.get)} does)")
        if min(asps[e] for e in ("Mass", "Space", "Time")) == asps["Mass"]:
            ok.append("Mass most cubic")
        else:
            failures.append(f"{name}: MASS is not its most cubic leaf - the Mass law says "
                            f"x, y and z sit closest together")
        print(f"    {name:20s} " + ", ".join(ok))

    # ── the stated limitation ──────────────────────────────────────────────────────────
    print("\n" + "=" * 78)
    print("STATED LIMITATION - where the law is silently trimmed")
    print("=" * 78)
    trimmed = []
    for name in sorted(NON_ADMITTING_PREFABS):
        for element in ELEMENTS:
            leaf = species.get(name, {}).get(element, {}).get("leaf")
            if not leaf:
                continue
            after = py_shape_leaf(leaf, consts["MassLeafVolume"] if element == "Mass"
                                  else consts["SpaceLeafVolume"] if element == "Space"
                                  else consts["NeutralLeafVolume"],
                                  consts["MassLeafAnisotropy"] if element == "Mass"
                                  else consts["SpaceLeafAnisotropy"] if element == "Space"
                                  else 1.0)
            # Only x/y reach a prism on this family (it reads LeafSize.x/y as a CROSS-SECTION
            # and takes its lengths from its own segment/reach), so only those can be trimmed.
            for axis, value in (("x", after[0]), ("y", after[1])):
                if value < PRISM_SCALE_MIN or value > PRISM_SCALE_MAX:
                    trimmed.append(f"{name} {element} {axis}={value:.3f}")
    if trimmed:
        print("    PhyllotacticFlora.AddHealthBlock does NOT call AdmitTargetScale "
              "(Docs/ECOSYSTEM.md §34.9),")
        print(f"    so PrismScaleAnimator clamps these into [{PRISM_SCALE_MIN}, "
              f"{PRISM_SCALE_MAX}] with no log:")
        for t in trimmed:
            print(f"      {t}")
        print("    These are the species' own cross-sections BEFORE the whorl multiplier, so")
        print("    the trim is an upper bound on what is lost. Reported, not gated: admitting")
        print("    the size is the correct fix and it changes the Hesperides garden's look,")
        print("    which is a play-test, not a build gate.")
    else:
        print("    none - every transformed leaf lands inside PrismScaleAnimator's window.")

    # ── verdict ────────────────────────────────────────────────────────────────────────
    print("\n" + "=" * 78)
    if failures:
        print(f"FAIL - {len(failures)} violation(s)")
        for f in failures:
            print(f"  * {f}")
        return 1 if args.check else 0
    print("PASS - the law is measured, run, neutral, and honoured by every exempt species.")
    return 0


def self_test() -> int:
    """Prove each gate can FAIL. A gate nobody has watched fail is a gate nobody should trust."""
    print("self-test: mutating the inputs and asserting each gate trips\n")
    ok = True

    # (a) a drifted constant
    text = LAW.read_text()
    backup = text
    mutated = text.replace("MassLeafVolume = 1.8347f", "MassLeafVolume = 1.0000f")
    assert mutated != text, "the constant moved - update the self-test"
    LAW.write_text(mutated)
    try:
        r = subprocess.run([sys.executable, __file__, "--check"],
                           capture_output=True, text=True)
        hit = r.returncode != 0 and "re-derive" in r.stdout
        print(f"  (a) constant drifted off the assets      -> {'FAILS (good)' if hit else 'MISSED'}")
        ok &= hit
    finally:
        LAW.write_text(backup)

    # (b) volume neutrality broken
    mutated = backup.replace("NeutralLeafVolume = 0.8778f", "NeutralLeafVolume = 1.5000f")
    LAW.write_text(mutated)
    try:
        r = subprocess.run([sys.executable, __file__, "--check"],
                           capture_output=True, text=True)
        hit = r.returncode != 0 and "average" in r.stdout
        print(f"  (b) four volumes no longer average to 1  -> {'FAILS (good)' if hit else 'MISSED'}")
        ok &= hit
    finally:
        LAW.write_text(backup)

    # (c) anisotropy stops being volume-exact
    mutated = backup.replace(
        "                size * Mathf.Pow(authored.z / mean, anisotropy));",
        "                size * Mathf.Pow(authored.z / mean, anisotropy) * 1.2f);")
    assert mutated != backup, "ShapeLeaf moved - update the self-test"
    LAW.write_text(mutated)
    try:
        r = subprocess.run([sys.executable, __file__, "--check"],
                           capture_output=True, text=True)
        hit = r.returncode != 0 and ("volume-exact" in r.stdout or "disagrees" in r.stdout)
        print(f"  (c) anisotropy moves volume              -> {'FAILS (good)' if hit else 'MISSED'}")
        ok &= hit
    finally:
        LAW.write_text(backup)

    # (d) an exempt species contradicting the law
    asset = LIFEFORMS / "Gyroid Flora Mass.asset"
    a_backup = asset.read_text()
    a_mut = a_backup.replace("LeafSize: {x: 7, y: 4.5, z: 3.5}",
                             "LeafSize: {x: 0.7, y: 0.45, z: 0.35}")
    assert a_mut != a_backup, "the gyroid Mass leaf moved - update the self-test"
    asset.write_text(a_mut)
    try:
        r = subprocess.run([sys.executable, __file__, "--check"],
                           capture_output=True, text=True)
        hit = r.returncode != 0 and "heaviest" in r.stdout
        print(f"  (d) an exempt species contradicts it     -> {'FAILS (good)' if hit else 'MISSED'}")
        ok &= hit
    finally:
        asset.write_text(a_backup)

    # (e) the control: unmutated, everything passes
    r = subprocess.run([sys.executable, __file__, "--check"], capture_output=True, text=True)
    hit = r.returncode == 0
    print(f"  (e) CONTROL: shipped tree unmutated      -> {'PASSES (good)' if hit else 'FAILS'}")
    if not hit:
        print(r.stdout[-3000:])
    ok &= hit

    print("\nself-test:", "OK" if ok else "BROKEN")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
