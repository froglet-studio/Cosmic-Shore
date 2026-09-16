#!/usr/bin/env python3
"""
Prove the SHIPPED MandelbulbLattice.cs against a fresh reference walk - site for site, normal
for normal.

    python3 Tools/Build/verify_mandelbulb_flora_tables.py            # verify + report
    python3 Tools/Build/verify_mandelbulb_flora_tables.py --check    # CI: exit 1 on any drift
    python3 Tools/Build/verify_mandelbulb_flora_tables.py --self-test # prove the gate can FAIL

WHY THIS IS A SEPARATE SCRIPT FROM THE MEASUREMENT
--------------------------------------------------
measure_mandelbulb_flora.py is the MODEL: it decides what this species costs and what its plate
should be. This script asks a different question - does the file that actually runs in the game
agree with that model? The transcription from a proven measurement into shipped code is the step
neither the measurement nor code review can see, and the gyroid paid five playtests for exactly
that gap (Docs/ECOSYSTEM.md 32.7 - a symmetry shortcut twinned 12 of 16 seed rotations by up to
179 degrees, and every static check passed).

So this does not parse the C# and re-derive anything. It COMPILES the shipped MandelbulbLattice.cs
against a UnityEngine stub and RUNS it (Tools/Build/mandelbulb_lattice_harness/), then compares
its own answers against a reference walk computed here from the implicit function. Same idiom as
the Regatta course harness.

WHAT IS PROVED
--------------
  1. the SEED site the plant starts from
  2. the exact SET of shell sites the 26-neighbour walk reaches - no missing site, no extra one
  3. the ORDER the walk reaches them in (so a growth front cannot silently re-order)
  4. every site's NORMAL, to 1e-4
  5. the PLATING: the same number of prisms, each standing for the same patch (compared by the
     patch's own seed CELL, which is an exact integer identity, in growth order), each with the
     same measured centre, frame and size
  6. that the walk reaches essentially the whole local shell (a plant can complete its form).
     Not ALL of it, and that is a measurement rather than a compromise: at the shipped pitch the
     set resolves a handful of detached specks and sealed-void walls that no walk from the seed
     can reach - 0.1% to 0.7% of the shell by element. The gate is a stated fraction, which is
     what makes the 6-connected negative control (which strands ~30%) still trip it.
  7. for each element the flora authors, not just the default

Needs a dotnet 8 SDK for the harness (a per-user install is fine - see the harness's run.sh).
Without one the script says so and exits 1 rather than quietly passing.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import measure_mandelbulb_flora as model  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
HARNESS = ROOT / "Tools/Build/mandelbulb_lattice_harness/run.sh"
LATTICE_CS = ROOT / "Assets/_Scripts/Controller/Environment/FloraAndFauna/MandelbulbLattice.cs"

NORMAL_TOLERANCE = 1e-4
# See point 6 above: the shell of a finely resolved fractal has islands.
MAX_UNREACHABLE_FRACTION = 0.02
# Plate geometry is derived in double precision on both sides and handed over as float, so this
# is a float-round-trip tolerance, not a modelling one. The IDENTITY of a plate is compared
# exactly (its patch's seed cell) - there is no tolerance on which cells belong to which prism.
PLATE_TOLERANCE = 1e-4


def run_harness(power, pitch, iterations, bailout, bound, rules, attempts=2):
    """Compile + run the SHIPPED lattice file and return its own answers.

    A harness that comes back EMPTY is reported as a harness failure, not fed to the JSON parser:
    `Expecting value: line 1 column 1` names nothing and looks like a drift in the file under
    test. It has happened once, under memory pressure from several concurrent runs, and it is
    retried once because a flaky gate that fails loudly is still a flaky gate.
    """
    argv = [str(HARNESS), str(power), repr(pitch), str(iterations), repr(bailout), str(bound),
            repr(rules["riser"]), repr(rules["coplanar"]), repr(rules["tau"]),
            str(rules["max_cells"]), repr(rules["pad"]), repr(rules["thickness"]),
            repr(rules["drop"])]
    last = ""
    for _ in range(max(1, attempts)):
        proc = subprocess.run(argv, capture_output=True, text=True)
        if proc.returncode != 0:
            detail = (proc.stdout or "") + (proc.stderr or "")
            raise RuntimeError(f"harness failed (exit {proc.returncode}):\n{detail.strip()[:4000]}")
        if proc.stdout.strip():
            return json.loads(proc.stdout)
        last = (proc.stderr or "").strip()[:2000]
    raise RuntimeError("harness exited 0 but printed nothing - it did not run the shipped file"
                       + (f":\n{last}" if last else ""))


def reference(power, pitch, iterations, bailout, bound, rules):
    """A fresh walk and plating from the implicit function - this file's own answer, not the C#'s."""
    bulb = model.Bulb(power, pitch, iterations, bailout)
    order, _, plates = model.plating(bulb, rules, bound)
    return bulb, order, plates


def compare_plates(ref_plates, got, label, problems):
    """Prove the PLATING, identity first. A plate's identity is the integer cell its patch grew
    from, so two platings can be compared cell for cell rather than position for position - which
    is what makes a merge difference report as a merge difference instead of as float drift."""
    got_plates = got.get("plates") or []
    if len(got_plates) != len(ref_plates):
        problems.append(f"{label}: shipped plating laid {len(got_plates)} prisms, "
                        f"reference laid {len(ref_plates)}")
    ref_seeds = [p["seed"] for p in ref_plates]
    got_seeds = [(q[0], q[1], q[2]) for q in got_plates]
    if ref_seeds != got_seeds:
        missing = set(ref_seeds) - set(got_seeds)
        extra = set(got_seeds) - set(ref_seeds)
        if missing or extra:
            problems.append(f"{label}: the plating covers different patches "
                            f"({len(missing)} only in the reference, {len(extra)} only in the "
                            f"shipped file, e.g. {sorted(missing or extra)[:3]})")
        else:
            first = next(i for i, (a, b) in enumerate(zip(got_seeds, ref_seeds)) if a != b)
            problems.append(f"{label}: the plating ORDER diverges at prism {first} "
                            f"(shipped {got_seeds[first]}, reference {ref_seeds[first]})")
        return 0.0, 0

    worst = 0.0
    cells_wrong = 0
    for ref, q in zip(ref_plates, got_plates):
        if q[3] != ref["cells"]:
            cells_wrong += 1
        fields = (list(ref["c"]) + list(ref["e"][0]) + list(ref["e"][1]) + list(ref["e"][2])
                  + list(ref["size"]))
        for i, want in enumerate(fields):
            worst = max(worst, abs(q[4 + i] - want))
    if cells_wrong:
        problems.append(f"{label}: {cells_wrong} prism(s) stand for a different number of cells "
                        f"than the reference")
    if worst > PLATE_TOLERANCE:
        problems.append(f"{label}: worst plate geometry error {worst:.2e} "
                        f"(tolerance {PLATE_TOLERANCE:g})")
    return worst, len(ref_plates)


def compare(bulb, ref_order, got, label, problems):
    ref_seed = ref_order[0]
    got_seed = tuple(got["seed"])
    if ref_seed != got_seed:
        problems.append(f"{label}: seed site {got_seed} != reference {ref_seed}")

    got_sites = [(s[0], s[1], s[2]) for s in got["sites"]]
    if len(got_sites) != len(ref_order):
        problems.append(f"{label}: shipped walk reached {len(got_sites)} sites, "
                        f"reference reached {len(ref_order)}")
    missing = set(ref_order) - set(got_sites)
    extra = set(got_sites) - set(ref_order)
    if missing:
        problems.append(f"{label}: {len(missing)} site(s) the reference reaches and the shipped "
                        f"file does not, e.g. {sorted(missing)[:3]}")
    if extra:
        problems.append(f"{label}: {len(extra)} site(s) the shipped file reaches and the "
                        f"reference does not, e.g. {sorted(extra)[:3]}")
    if not missing and not extra and got_sites != ref_order:
        first = next(i for i, (a, b) in enumerate(zip(got_sites, ref_order)) if a != b)
        problems.append(f"{label}: the walk ORDER diverges at index {first} "
                        f"(shipped {got_sites[first]}, reference {ref_order[first]})")

    worst, worst_site = 0.0, None
    for entry in got["sites"]:
        site = (entry[0], entry[1], entry[2])
        if site not in set(ref_order):
            continue
        rn = bulb.normal(site)
        gn = (entry[3], entry[4], entry[5])
        err = math.sqrt(sum((rn[i] - gn[i]) ** 2 for i in range(3)))
        if err > worst:
            worst, worst_site = err, site
    if worst > NORMAL_TOLERANCE:
        problems.append(f"{label}: worst normal error {worst:.2e} at {worst_site} "
                        f"(tolerance {NORMAL_TOLERANCE:g})")
    return worst


def full_shell(bulb, bound):
    """Every locally-shell site in the bound - what the walk must be able to reach."""
    rng = range(-bound, bound + 1)
    return {s for s in ((i, j, k) for i in rng for j in rng for k in rng) if bulb.is_shell(s)}


def verify(verbose=True):
    a = model.authored()
    pitch, iters, bail = a["pitch"], a["iterations"], a["bailout"]
    rules = a["rules"]
    bound = model.bound_for(pitch)
    problems = []

    if verbose:
        print(f"Verifying {LATTICE_CS.relative_to(ROOT)} against a fresh reference walk")
        print(f"  pitch {pitch}  iterations {iters}  bailout {bail:g}  bound {bound}\n")
        print(f"  {'element':<8} {'power':>5} {'sites':>7} {'prisms':>7} {'order':>7} "
              f"{'normals':>11} {'plates':>11} {'connected':>10}")

    for elem in ("Charge", "Mass", "Space", "Time"):
        power = a["powers"].get(elem)
        if power is None:
            problems.append(f"{elem}: no authored power in MandelbulbFlora.cs")
            continue
        label = f"{elem} (power {power})"
        got = run_harness(power, pitch, iters, bail, bound, rules)
        bulb, ref_order, ref_plates = reference(power, pitch, iters, bail, bound, rules)
        before = len(problems)
        worst = compare(bulb, ref_order, got, label, problems)
        plate_err, plate_count = compare_plates(ref_plates, got, label, problems)

        shell = full_shell(bulb, bound)
        reached = set(ref_order)
        unreached = shell - reached
        stranded = len(unreached) / max(1, len(shell))
        if stranded > MAX_UNREACHABLE_FRACTION:
            problems.append(f"{label}: the walk cannot reach {len(unreached)} of the "
                            f"{len(shell)} shell sites ({100*stranded:.1f}%, over the stated "
                            f"{100*MAX_UNREACHABLE_FRACTION:.0f}%) - a plant could never "
                            f"complete its form")

        if verbose:
            ok = len(problems) == before
            print(f"  {elem:<8} {power:>5} {len(ref_order):>7} {plate_count:>7} "
                  f"{'ok' if ok else 'DRIFT':>7} {worst:>11.2e} {plate_err:>11.2e} "
                  f"{100*stranded:>9.2f}%")

    return problems


def self_test():
    """A gate nobody has watched fail is a gate nobody should trust.

    Mutates a COPY of the shipped lattice file in place, re-runs the verification, and asserts it
    reports drift - once for a membership change and once for an orientation change. The original
    is restored whatever happens.
    """
    print("self-test: proving the verifier reports drift it should report\n")
    original = LATTICE_CS.read_text()
    backup = Path(tempfile.gettempdir()) / "MandelbulbLattice.cs.verifybak"
    backup.write_text(original)

    controls = [
        ("membership (bailout widened)",
         "if (r > Bailout) { inside = false; break; }",
         "if (r > Bailout * 1.5) { inside = false; break; }"),
        ("orientation (normal census restricted to faces)",
         "foreach (var d in Neighbour26)\n            {\n                if (Inside(site + d)) continue;",
         "foreach (var d in Face6)\n            {\n                if (Inside(site + d)) continue;"),
        ("connectivity (growth adjacency reduced to 6)",
         "public static readonly Vector3Int[] Neighbour26 = BuildNeighbour26();",
         "public static readonly Vector3Int[] Neighbour26 = Face6;"),
        ("plating (riser selection inverted)",
         "if (1.0 - Math.Abs(radial) >= rules.RiserBias) selected.Add(site);",
         "if (1.0 - Math.Abs(radial) <= rules.RiserBias) selected.Add(site);"),
        ("plating (patch merge disabled)",
         "while (frontier.Count > 0 && patch.Count < rules.MaxCells)",
         "while (frontier.Count > 0 && patch.Count < 1)"),
        ("plate frame (outward orientation dropped)",
         "        static bool PointsInward(double[] normal, Vector3 census, double[] centroid)\n        {\n",
         "        static bool PointsInward(double[] normal, Vector3 census, double[] centroid)\n        {\n            return false;\n"),
    ]

    failures = []
    try:
        for name, needle, replacement in controls:
            if needle not in original:
                failures.append(f"control '{name}': anchor text not found - the control is stale "
                                f"and proves nothing")
                continue
            LATTICE_CS.write_text(original.replace(needle, replacement, 1))
            try:
                problems = verify(verbose=False)
            except RuntimeError as exc:
                problems = [f"harness refused the mutated file: {exc}"]
            if problems:
                print(f"  OK   {name}\n       -> {problems[0][:150]}")
            else:
                failures.append(f"control '{name}' did NOT trip the verifier")
            LATTICE_CS.write_text(original)

        # And the unmutated file must PASS, or the controls above prove nothing.
        clean = verify(verbose=False)
        if clean:
            failures.append(f"the UNMUTATED shipped file does not verify: {clean[0]}")
        else:
            print("  OK   the unmutated shipped file verifies clean")
    finally:
        LATTICE_CS.write_text(original)

    if failures:
        print("\nSELF-TEST FAILED")
        for f in failures:
            print(f"  - {f}")
        return 1
    print("\nself-test passed: every control trips the verifier, and the shipped file passes.")
    return 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="exit 1 on drift")
    ap.add_argument("--self-test", action="store_true", help="prove the gate can fail")
    args = ap.parse_args()

    if not HARNESS.exists():
        print(f"missing harness: {HARNESS}")
        return 1
    dotnet = Path(os.environ.get("DOTNET_ROOT", Path.home() / ".dotnet")) / "dotnet"
    if not dotnet.exists() and not shutil.which("dotnet"):
        print("verify_mandelbulb_flora_tables: no dotnet 8 SDK found, so the SHIPPED C# was NOT\n"
              "run and nothing was proved. Install one (per-user is fine):\n"
              "  bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh) --channel 8.0 "
              "--install-dir $HOME/.dotnet --no-path")
        return 1

    if args.self_test:
        return self_test()

    try:
        problems = verify()
    except RuntimeError as exc:
        print(f"\nFAIL\n  - {exc}")
        return 1

    if problems:
        print("\nFAIL - the shipped lattice does not match the model")
        for p in problems:
            print(f"  - {p}")
        return 1
    print("\nOK - the shipped MandelbulbLattice reproduces the reference walk exactly.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
