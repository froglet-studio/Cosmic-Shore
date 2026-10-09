#!/usr/bin/env python3
"""
Find first-party code that allocates managed memory EVERY FRAME.

WHY THIS EXISTS
---------------
"GC / frame is too high" is a recurring question and the obvious answer — a
`new` inside `Update` — is usually NOT where the bytes are. This reader covers
the three places per-frame allocation actually hides, so a session can rule the
whole of first-party code in or out in about two seconds instead of re-deriving
the search:

  1. Update / LateUpdate / FixedUpdate / OnGUI bodies.
  2. `yield return new WaitForSeconds(...)` inside a LOOP - one allocation per
     iteration, forever, per live instance. (Docs/archive/PERFORMANCE_LOG_2026.md
     Tier 0b; Flora.GrowCoroutine and LifeForm.ShieldRegenCoroutine were this.)
  3. Coroutines that `yield return null` in a loop - these run every frame too
     and are invisible to a scan that only looks at Update.

READ THE ARITHMETIC BEFORE THE LIST.  The tool prints, for the GC/frame figure
you pass it, how MANY allocations of each size that number implies. A measured
198 KB/frame needs ~7,200 WaitForSeconds per frame (~224,000/sec) - which no
population in this project can produce, so a long list of category-2 hits is
not the answer to that number however satisfying it looks. ~200 KB/frame is a
FEW LARGE allocations (an array sized to the population), not thousands of
small ones. Sizing the target first is what stops a week of correct, irrelevant
fixes.

This is a READER: it writes nothing and has no ship panel (Docs/TOOLING.md).

    python3 Tools/Build/scan_perframe_allocations.py [--gc-kb 197.9] [--all]

Exit code is always 0 - the findings need a human to rank, and a gate that
fails on `$"..."` inside an Update would cry wolf on every debug readout.
"""
import argparse
import os
import re
import sys

ROOT = os.path.join("Assets", "_Scripts")
SKIP_DIRS = (os.sep + "Editor", os.sep + "Tests")
COMMENT = re.compile(r"^\s*(//|///|\*|/\*)")

FRAME_ENTRY = re.compile(r"\bvoid\s+(Update|LateUpdate|FixedUpdate|OnGUI)\s*\(\s*\)")
COROUTINE = re.compile(r"\bIEnumerator\s+(\w+)\s*\(")
YIELD_NULL = re.compile(r"yield\s+return\s+null\s*;")
YIELD_NEW = re.compile(
    r"yield\s+return\s+new\s+"
    r"(WaitForSeconds|WaitForSecondsRealtime|WaitUntil|WaitWhile|"
    r"WaitForFixedUpdate|WaitForEndOfFrame)\b"
)
LOOP_HEAD = re.compile(r"\s*(while|for|foreach|do)\b")
MEMBER_HEAD = re.compile(r"\s*(private|public|protected|internal|IEnumerator|void)\b")

# Only APIs that allocate a whole ARRAY or LIST per call. Deliberately NOT
# `$"..."`, `.ToString()` or lambdas: at ~30 bytes each they cannot reach a
# six-figure per-frame figure, and including them buries the ones that can.
BIG_ALLOC = [
    ("FindObjects*",            re.compile(r"\bFindObjects(ByType|OfType)\s*<")),
    ("GetComponentsInChildren", re.compile(r"\bGetComponentsInChildren\s*<")),
    ("GetComponentsInParent",   re.compile(r"\bGetComponentsInParent\s*<")),
    ("GetComponents<>",         re.compile(r"\bGetComponents\s*<")),
    (".sharedMaterials",        re.compile(r"\.sharedMaterials\b")),
    (".materials",              re.compile(r"(?<!shared)\.materials\b")),
    ("mesh geometry array",     re.compile(r"\.(vertices|triangles|normals|uv|colors|tangents)\b")),
    ("Physics *All/Overlap",    re.compile(r"\bPhysics\.(OverlapSphere|OverlapBox|OverlapCapsule|"
                                           r"RaycastAll|SphereCastAll|BoxCastAll)\s*\(")),
    ("ToArray/ToList",          re.compile(r"\.To(Array|List)\s*\(")),
    ("new collection",          re.compile(r"\bnew\s+(List|Dictionary|HashSet|Queue|Stack)\s*<")),
    ("new T[n]",                re.compile(r"\bnew\s+[A-Za-z_][\w\.<>]*\s*\[\s*[^\]\s]")),
]


def source_files():
    for dirpath, _, filenames in os.walk(ROOT):
        if any(skip in dirpath for skip in SKIP_DIRS):
            continue
        for name in filenames:
            if name.endswith(".cs"):
                yield os.path.join(dirpath, name)


def method_bodies(src, entry_regex):
    """Yield (name, body, first_line) for each method the regex opens."""
    for match in entry_regex.finditer(src):
        open_brace = src.find("{", match.end())
        if open_brace < 0:
            continue
        depth, cursor = 0, open_brace
        while cursor < len(src):
            if src[cursor] == "{":
                depth += 1
            elif src[cursor] == "}":
                depth -= 1
                if depth == 0:
                    break
            cursor += 1
        yield match.group(1), src[open_brace:cursor + 1], src[:open_brace].count("\n") + 1


def strip_comments(body):
    return "\n".join(l for l in body.split("\n") if not COMMENT.match(l))


def scan_big(body):
    clean = strip_comments(body)
    return [f"{label}x{len(rx.findall(clean))}" for label, rx in BIG_ALLOC if rx.search(clean)]


def scan_yield_in_loop(path, lines):
    """A `yield return new ...` whose nearest enclosing block is a loop."""
    hits = []
    for i, line in enumerate(lines, start=1):
        if COMMENT.match(line):
            continue
        m = YIELD_NEW.search(line)
        if not m:
            continue
        indent = len(line) - len(line.lstrip())
        for j in range(i - 2, max(0, i - 45), -1):
            prev = lines[j]
            if not prev.strip() or COMMENT.match(prev):
                continue
            prev_indent = len(prev) - len(prev.lstrip())
            if prev_indent >= indent:
                continue
            if LOOP_HEAD.match(prev):
                hits.append((path, i, m.group(1), prev.strip()[:64], line.strip()[:72]))
                break
            if MEMBER_HEAD.match(prev):
                break  # reached the method header without passing a loop
    return hits


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--gc-kb", type=float, default=0.0,
                    help="measured GC/frame in KB - prints how many allocations that implies")
    ap.add_argument("--fps", type=float, default=30.0, help="frame rate the GC figure was read at")
    ap.add_argument("--all", action="store_true", help="print every hit, not just the first 40 per section")
    args = ap.parse_args()

    if not os.path.isdir(ROOT):
        print(f"run me from the repo root (no {ROOT})", file=sys.stderr)
        return 0

    if args.gc_kb > 0:
        total = args.gc_kb * 1024
        print(f"TARGET  {args.gc_kb:.1f} KB/frame = {total:,.0f} bytes at {args.fps:.0f} fps")
        for label, size in (("WaitForSeconds", 28), ("small List<T>", 64), ("Material[2]", 40)):
            n = total / size
            print(f"  as {label:<16} (~{size} B) -> {n:>9,.0f} allocations EVERY frame "
                  f"= {n * args.fps:>12,.0f}/sec")
        print("  A six-figure per-frame figure is a FEW LARGE allocations, not many small ones.\n")

    per_frame, per_frame_coro, yield_loops = [], [], []
    for path in source_files():
        src = open(path, encoding="utf-8", errors="replace").read()
        for name, body, line in method_bodies(src, FRAME_ENTRY):
            found = scan_big(body)
            if found:
                per_frame.append((path, line, name, found))
        for name, body, line in method_bodies(src, COROUTINE):
            if not YIELD_NULL.search(strip_comments(body)):
                continue  # not a per-frame coroutine
            found = scan_big(body)
            if found:
                per_frame_coro.append((path, line, name, found))
        yield_loops += scan_yield_in_loop(path, src.split("\n"))

    cap = None if args.all else 40

    def section(title, rows, fmt):
        print(f"=== {title}: {len(rows)}")
        for row in sorted(rows)[:cap]:
            print("    " + fmt(row))
        if cap and len(rows) > cap:
            print(f"    ... {len(rows) - cap} more (--all)")
        print()

    section("LARGE allocations in Update/LateUpdate/FixedUpdate/OnGUI", per_frame,
            lambda r: f"{r[0]}:{r[1]}  {r[2]}()  ->  " + ", ".join(r[3]))
    section("LARGE allocations in per-FRAME coroutines (yield return null)", per_frame_coro,
            lambda r: f"{r[0]}:{r[1]}  {r[2]}()  ->  " + ", ".join(r[3]))
    section("`yield return new ...` INSIDE A LOOP (one alloc per iteration, per instance)",
            yield_loops,
            lambda r: f"{r[0]}:{r[1]}  [{r[2]}]  loop: {r[3]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
