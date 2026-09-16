#!/usr/bin/env python3
"""Every runtime Instantiate of a FAUNA PREFAB must reach FaunaNetworkSync.ServerSpawn.

WHY THIS GATE EXISTS
--------------------
`QuadFish.prefab` and `TadPoleFauna.prefab` carry a `NetworkObject`, and an
UN-SPAWNED `NetworkObject` is adopted by Netcode as an IN-SCENE PLACED object
keyed `(GlobalObjectIdHash, sceneHandle)`.  Every instance of one prefab shares
that hash, so the SECOND un-spawned instance in one scene makes
`NetworkSceneManager.PopulateScenePlacedObjects` throw - the moment a host
starts OR A CLIENT SYNCHRONIZES.  After that the host can never synchronize
anyone for the rest of the session.

`FaunaNetworkSync.ServerSpawn` is the one call that resolves this: it spawns the
NetworkObject for a replicated species and STRIPS it (`NeutralizeStray`) for
every other case.  A producer that instantiates a fauna prefab and skips it
leaves a live hazard behind.

This has now shipped TWICE - `Docs/PartySystem/BUGS.md` B16, then B5 when three
producers (`ModePreviewArena`, `LifeformMatrixToy`, `Microscene`) reached
`CellLifeSpawnerBase.SpawnFaunaWithDomain` directly and so bypassed a seam that
lived one level up in `SpawnFaunaBanded`.  The seam has since moved down to the
one `Instantiate` every producer reaches, which closes those three by
construction - but a NEW producer can still call `Instantiate` itself.  Three
did: `LightFaunaManager` (a school of QuadFish), `BoidManager` (100-150
TadPoleFauna, both of them NetworkObject-carrying prefabs) and `WormFauna`
(five producers).  That is what this gate watches, and all three were found by
RUNNING it rather than by reading the code.

WHAT IT CHECKS
--------------
For every `Instantiate(<expr>, ...)` whose argument names a creature, the
enclosing METHOD must also contain a `FaunaNetworkSync.ServerSpawn` call.

"Names a creature" is matched textually, and the test WIDENS inside a file that
declares a `Fauna` subclass, because a creature class instantiating a prefab is
instantiating a creature:

  * anywhere: the expression contains "fauna" AND ends in "prefab" - so
    `faunaPrefab`, `lightFaunaPrefab` and `cfg.FaunaPrefab` match while
    `definition.PreviewFauna` (a CONFIG asset, not a creature) does not;
  * in a creature file: ending in "prefab" is enough (`headPrefab`,
    `bodyPrefab`, `tailPrefab`, `boidPrefab`), and `Instantiate(this, ...)` -
    how a colony splits itself into a second population - counts too.

That widening is what reaches `WormFauna` and `BoidManager`, whose producers
name no fauna at all.  It stays clean because what a creature class
instantiates that is NOT a creature does not end in "prefab": `Boid.cs`
instantiates `healthPrism`, which is exactly a negative control in the
self-test.

Method extent is found by expanding outward one brace block at a time until a
block containing the call is found, or until the enclosing block's header names
a `class`, `struct` or `namespace`, at which point the site has no covering
call and is reported.

It is a TEXTUAL check on purpose: it needs no Unity assemblies and no symbol
table, which is what lets it run in the same second as the other gates.  Two
costs, both stated rather than papered over:

  * OUTSIDE a creature file it stays narrow, so a producer there that assigns a
    creature prefab to a neutrally-named local is invisible to it;
  * it asks about the enclosing METHOD, so a producer that hands its newborn
    straight to a funnel one call away is reported even when that funnel is
    correct.  That is deliberate - a sibling method holding the seam is one of
    the negative controls - and the right answer is to move the `Instantiate`
    INTO the funnel, which is what `WormFauna.AddSegmentToChain` now does.

The runtime backstop for anything this misses is
`NetworkSceneObjectGuard.Sweep` at connection approval.

Usage:
    python3 Tools/Build/check_fauna_replication_seam.py
    python3 Tools/Build/check_fauna_replication_seam.py --self-test
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCAN_ROOT = REPO / "Assets" / "_Scripts"

# Instantiate(<expr>  -- <expr> is an identifier possibly qualified with dots.
INSTANTIATE = re.compile(r"\bInstantiate\s*(?:<[^>]*>\s*)?\(\s*([A-Za-z_][\w.]*|this)")
SERVER_SPAWN = re.compile(r"\bFaunaNetworkSync\s*\.\s*ServerSpawn\s*\(")
TYPE_HEADER = re.compile(r"\b(class|struct|interface|namespace|enum)\b")

# A file whose own class IS a creature. Inside one, "prefab" alone is enough - a
# creature class instantiating a prefab is instantiating a creature - which is what
# reaches WormFauna's headPrefab/bodyPrefab/tailPrefab and BoidManager's boidPrefab.
# Outside one the test has to stay narrow, because "prefab" project-wide is every
# projectile, prism and UI row.
FAUNA_SUBCLASS = re.compile(r"\bclass\s+\w+\s*:\s*(?:Fauna|LightFauna|WormFauna|WormSegmentFauna)\b")


def names_a_fauna_prefab(expr: str, in_creature_file: bool) -> bool:
    low = expr.lower()
    if low.endswith("prefab"):
        # Inside a creature class, any prefab it spawns is a creature. `healthPrism`
        # and friends do not end in "prefab", which is what keeps this clean.
        return in_creature_file or "fauna" in low
    # `Instantiate(this, ...)` is how a colony splits itself into a second population.
    return in_creature_file and expr == "this"


def strip_noise(src: str) -> str:
    """Blank out comments and string/char literals, preserving offsets.

    Braces and the word ServerSpawn inside a comment must not steer the scan -
    this file's own doc comments would otherwise satisfy the check for it.
    """
    out = list(src)
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c == "/" and i + 1 < n and src[i + 1] == "/":
            while i < n and src[i] != "\n":
                out[i] = " "
                i += 1
        elif c == "/" and i + 1 < n and src[i + 1] == "*":
            while i < n and not (src[i] == "*" and i + 1 < n and src[i + 1] == "/"):
                if src[i] != "\n":
                    out[i] = " "
                i += 1
            for _ in range(2):
                if i < n:
                    if src[i] != "\n":
                        out[i] = " "
                    i += 1
        elif c in ("\"", "'"):
            quote = c
            i += 1
            while i < n and src[i] != quote:
                if src[i] == "\\":
                    out[i] = " "
                    i += 1
                    if i < n and src[i] != "\n":
                        out[i] = " "
                        i += 1
                    continue
                if src[i] != "\n":
                    out[i] = " "
                i += 1
            if i < n:
                out[i] = " "
                i += 1
        else:
            i += 1
    return "".join(out)


def brace_map(src: str) -> tuple[list[int], dict[int, int]]:
    """Return (open-brace stack per index is too big; instead) -> (opens, close_of).

    `opens` is every '{' index in order; `close_of` maps each '{' to its '}'.
    """
    stack: list[int] = []
    close_of: dict[int, int] = {}
    opens: list[int] = []
    for i, c in enumerate(src):
        if c == "{":
            stack.append(i)
            opens.append(i)
        elif c == "}" and stack:
            close_of[stack.pop()] = i
    for o in stack:  # unbalanced file: treat as running to EOF
        close_of[o] = len(src)
    return opens, close_of


def enclosing_blocks(pos: int, opens: list[int], close_of: dict[int, int]) -> list[int]:
    """Every '{' whose block contains pos, innermost first."""
    found = [o for o in opens if o < pos <= close_of[o]]
    found.sort(reverse=True)
    return found


def block_header(src: str, open_brace: int) -> str:
    """The text introducing this block: everything back to the previous structural
    delimiter. Deliberately NOT "the previous two lines" - a method declared directly
    under `class S {` would pick the class line up and be misread as a type body.
    """
    start = max(src.rfind(c, 0, open_brace) for c in "{};")
    return src[start + 1:open_brace]


def check_source(src: str, path_label: str) -> list[str]:
    clean = strip_noise(src)
    opens, close_of = brace_map(clean)
    in_creature_file = bool(FAUNA_SUBCLASS.search(clean))
    problems: list[str] = []

    for m in INSTANTIATE.finditer(clean):
        expr = m.group(1)
        if not names_a_fauna_prefab(expr, in_creature_file):
            continue
        pos = m.start()
        line_no = clean.count("\n", 0, pos) + 1

        covered = False
        for o in enclosing_blocks(pos, opens, close_of):
            # Stop BEFORE testing a type body. A class that calls ServerSpawn in some
            # OTHER method is not coverage for this site - and testing coverage first
            # would accept exactly that (the sibling-method self-test case).
            if TYPE_HEADER.search(block_header(clean, o)):
                break
            if SERVER_SPAWN.search(clean[o:close_of[o]]):
                covered = True
                break
        if not covered:
            problems.append(
                f"{path_label}:{line_no}: Instantiate({expr}, ...) has no "
                f"FaunaNetworkSync.ServerSpawn in its method.\n"
                f"    An un-spawned fauna NetworkObject breaks synchronization for every\n"
                f"    later joiner (Docs/PartySystem/BUGS.md B16, B5). Call\n"
                f"    FaunaNetworkSync.ServerSpawn(<the new fauna>) after Initialize\n"
                f"    (and after AssignLineage, if this site binds one)."
            )
    return problems


SELF_TEST_CASES = [
    # (label, source, expect_problem)
    ("covered: spawner shape", """
class S {
  void Spawn() {
    var pop = Object.Instantiate(faunaPrefab, pos, Quaternion.identity);
    pop.Initialize(host);
    FaunaNetworkSync.ServerSpawn(pop);
  }
}
""", False),
    ("BUG: the B5 shape - no seam in the method", """
class S {
  void Spawn() {
    var pop = Object.Instantiate(faunaPrefab, pos, Quaternion.identity);
    pop.Initialize(host);
  }
}
""", True),
    ("covered: seam inside the loop", """
class S {
  void Spawn() {
    for (int i = 0; i < n; i++) {
      LightFauna f = Instantiate(lightFaunaPrefab, p, r, transform);
      f.Initialize(cell);
      FaunaNetworkSync.ServerSpawn(f);
    }
  }
}
""", False),
    ("covered: qualified prefab expression", """
class S {
  void Spawn() {
    var child = Instantiate(cfg.FaunaPrefab, pos, Quaternion.identity);
    child.AssignLineage(host, cfg);
    FaunaNetworkSync.ServerSpawn(child);
  }
}
""", False),
    ("BUG: a SIBLING method has the seam, this one does not", """
class S {
  void Covered() {
    var a = Instantiate(faunaPrefab, p, q);
    FaunaNetworkSync.ServerSpawn(a);
  }
  void Leaky() {
    var b = Instantiate(faunaPrefab, p, q);
    b.Initialize(host);
  }
}
""", True),
    ("not a creature: a CONFIG clone must not fire", """
class S {
  void Preview() {
    var clone = Object.Instantiate(definition.PreviewFauna);
  }
}
""", False),
    ("not a fauna: an unrelated prefab must not fire", """
class S {
  void Lay() {
    var p = Instantiate(prismPrefab, pos, rot);
  }
}
""", False),
    ("creature file: a neutrally-named prefab DOES fire", """
class WormFauna : Fauna {
  void Grow() {
    var seg = Instantiate(headPrefab, pos, rot);
    seg.Initialize(cell);
  }
}
""", True),
    ("creature file: Instantiate(this) is a second population", """
class WormFauna : Fauna {
  void Split() {
    var colony = Instantiate(this, pos, rot);
    colony.Initialize(cell);
  }
}
""", True),
    ("creature file: a covered neutrally-named prefab is clean", """
class BoidManager : Fauna {
  void Spawn() {
    Boid b = Instantiate(boidPrefab, pos, rot, transform);
    b.Initialize(cell);
    FaunaNetworkSync.ServerSpawn(b);
  }
}
""", False),
    ("creature file: a PRISM is not a creature", """
class Boid : Fauna {
  void AddBlock() {
    var newBlock = Instantiate(healthPrism, transform.position, transform.rotation, transform);
  }
}
""", False),
    ("NOT a creature file: Instantiate(this) must not fire", """
class Spawner : MonoBehaviour {
  void Clone() {
    var copy = Instantiate(this, pos, rot);
  }
}
""", False),
    ("a commented-out seam is NOT coverage", """
class S {
  void Spawn() {
    var pop = Instantiate(faunaPrefab, p, q);
    // FaunaNetworkSync.ServerSpawn(pop);
  }
}
""", True),
    ("a seam named only in a STRING is NOT coverage", """
class S {
  void Spawn() {
    var pop = Instantiate(faunaPrefab, p, q);
    Log("call FaunaNetworkSync.ServerSpawn(pop) here");
  }
}
""", True),
]


def self_test() -> int:
    failures = 0
    for label, src, expect in SELF_TEST_CASES:
        got = bool(check_source(src, "<self-test>"))
        ok = got == expect
        print(f"  [{'ok' if ok else 'FAIL'}] {label} (expected {'a finding' if expect else 'clean'}, got {'a finding' if got else 'clean'})")
        if not ok:
            failures += 1
    print()
    if failures:
        print(f"self-test: {failures} case(s) FAILED")
        return 1
    print(f"self-test: OK ({len(SELF_TEST_CASES)} cases, "
          f"{sum(1 for c in SELF_TEST_CASES if c[2])} of them negative controls)")
    return 0


def main() -> int:
    if "--self-test" in sys.argv:
        return self_test()

    if not SCAN_ROOT.is_dir():
        print(f"fauna-replication-seam check: nothing to scan at {SCAN_ROOT}")
        return 0

    problems: list[str] = []
    scanned = 0
    for path in sorted(SCAN_ROOT.rglob("*.cs")):
        try:
            src = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        scanned += 1
        if "Instantiate" not in src:
            continue
        problems.extend(check_source(src, str(path.relative_to(REPO))))

    if problems:
        print("fauna-replication-seam check: PROBLEM(S) FOUND\n")
        for p in problems:
            print(p + "\n")
        print(f"{len(problems)} problem(s) across {scanned} files scanned.")
        return 1

    print(f"fauna-replication-seam check: OK ({scanned} files scanned)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
