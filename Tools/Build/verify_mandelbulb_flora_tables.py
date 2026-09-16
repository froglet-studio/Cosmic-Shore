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
  5. that the walk is CONNECTED and equals the full local shell (a plant can reach its whole form)
  6. for each element the flora authors, not just the default

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


def run_harness(power, pitch, iterations, bailout, bound):
    """Compile + run the SHIPPED lattice file and return its own answers."""
    proc = subprocess.run(
        [str(HARNESS), str(power), repr(pitch), str(iterations), repr(bailout), str(bound)],
        capture_output=True, text=True)
    if proc.returncode != 0:
        detail = (proc.stdout or "") + (proc.stderr or "")
        raise RuntimeError(f"harness failed (exit {proc.returncode}):\n{detail.strip()[:4000]}")
    return json.loads(proc.stdout)


def reference(power, pitch, iterations, bailout, bound):
    """A fresh walk from the implicit function - this file's own answer, not the C#'s."""
    bulb = model.Bulb(power, pitch, iterations, bailout)
    order = bulb.walk(bound)
    return bulb, order


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
    bound = model.bound_for(pitch)
    problems = []

    if verbose:
        print(f"Verifying {LATTICE_CS.relative_to(ROOT)} against a fresh reference walk")
        print(f"  pitch {pitch}  iterations {iters}  bailout {bail:g}  bound {bound}\n")
        print(f"  {'element':<8} {'power':>5} {'sites':>7} {'order':>7} {'normals':>11} "
              f"{'connected':>10}")

    for elem in ("Charge", "Mass", "Space", "Time"):
        power = a["powers"].get(elem)
        if power is None:
            problems.append(f"{elem}: no authored power in MandelbulbFlora.cs")
            continue
        label = f"{elem} (power {power})"
        got = run_harness(power, pitch, iters, bail, bound)
        bulb, ref_order = reference(power, pitch, iters, bail, bound)
        before = len(problems)
        worst = compare(bulb, ref_order, got, label, problems)

        shell = full_shell(bulb, bound)
        reached = set(ref_order)
        unreached = shell - reached
        if unreached:
            problems.append(f"{label}: the walk cannot reach {len(unreached)} of the "
                            f"{len(shell)} shell sites - a plant could never complete its form")

        if verbose:
            ok = len(problems) == before
            print(f"  {elem:<8} {power:>5} {len(ref_order):>7} "
                  f"{'ok' if ok else 'DRIFT':>7} {worst:>11.2e} "
                  f"{'yes' if not unreached else 'NO':>10}")

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
