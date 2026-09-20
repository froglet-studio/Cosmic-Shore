#!/usr/bin/env python3
"""Fail on any claim that a prism SHIELD costs an always-on collider.

The claim is false and has been measured false three separate times (SKEIN.md's survey
refutation, BREAKWATER.md's go/no-go gate that did not exist, ECOSYSTEM.md 1288). A shield
swaps the MESH and the mass, never the collider: `shieldMeshCollider.enabled = true` appears
nowhere in the project - the field is declared on `PrismOctahedronShield` and
`PrismStellatedOctahedronShield` and assigned `= false` at four sites, never `true` - and both
components' `sharedMesh` writes land on the MeshFilter, so there is not even a convex cook.

It still regrew to ~20 live sites across code comments, mode docs and ECOSYSTEM.md, because the
refutations were written in new places instead of sweeping the old ones. That is CLAUDE.md's own
rule about a deleted SDK, one level down: *a retired claim goes on looking present for as long as
anything still describes the project in its terms.* This gate is the sweep, held.

It is deliberately NARROW. The collider-cost phrases below appear in this project for exactly one
reason, so the pattern needs no shield keyword; what it needs is to let a REFUTATION or a
historical narration through, which is what `EXCULPATING` does over a +/-2-line window.

Usage:
    python3 Tools/Build/check_shield_collider_claims.py [--check]
    python3 Tools/Build/check_shield_collider_claims.py --self-test
"""

import argparse
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# Where a claim about the collider budget can live. Prose counts: a comment is what the next
# author reads, and the escaped instances were roughly half comment and half doc.
SCAN_DIRS = ("Assets", "Docs", "Tools")
SCAN_FILES = ("CLAUDE.md", "AGENTS.md", "README.md")
SCAN_EXTS = (".cs", ".md", ".py", ".hlsl", ".shader")

# The shape of the claim: a cost phrase AND a shield term in the same window. The cost phrase
# alone is not enough - a flora/fauna HEART really is one always-on `SphereCollider` per live
# lifeform, and several correct budget lines say so. That was the gate's first false-positive
# class and the reason SHIELD_TERM exists.
CLAIM = re.compile(
    r"always[- ]on\s+(convex\s+)?mesh\s*collider"
    r"|always[- ]on\s+collider"
    r"|permanent\s+mesh\s+collider"
    r"|convex\s+mesh\s*collider\s+(that|the)\s+collider[- ]lod"
    r"|swaps?\s+(the\s+box|off\s+the\s+lod[- ]cullable\s+boxcollider)",
    re.I,
)

SHIELD_TERM = re.compile(
    r"shield|stella|octahedron|armour|armor",
    re.I,
)

# A line that REFUTES the claim, narrates it as retired, or prices it at zero is correct prose
# and must not fire. Checked over the hit line plus two lines either side, because these
# documents wrap at ~100 columns and the refutation routinely lands on the next line.
EXCULPATING = re.compile(
    r"never the collider"
    r"|appears nowhere"
    r"|\bREFUTED\b"
    r"|\bis FALSE\b|\bwas false\b|\bcurrently FALSE\b"
    r"|used to (claim|read|say)"
    r"|earlier version of this"
    r"|(that |the )?cost does not exist|no such cost"
    r"|earlier always[- ]on"
    r"|would (not|otherwise)"
    r"|\bis gone\b"
    r"|zero\s+(extra\s+)?(always[- ]on\s+)?(mesh\s*)?collider"
    r"|\bzero\s+always[- ]on|\bno\s+always[- ]on"
    r"|collider[- ]free"
    r"|no (extra|convex) collider"
    r"|no convex (mesh\s*collider|cook)"
    r"|\+\s*0\b"
    r"|check_shield_collider_claims",
    re.I,
)

# Inline markup sits INSIDE the phrase - `always-on convex <c>MeshCollider</c>` and
# `always-on convex \`MeshCollider\`s` are both real shipped spellings - so every line is
# stripped of C# doc tags, backticks and bold markers before matching. The gate's first version
# missed the original PrismKind.cs comment for exactly this reason.
# NOTE: it strips tag SYNTAX and keeps the words inside, because `<see cref="Shielded"/>` is
# where the shield keyword usually lives - a blanket `</?see[^>]*>` erased the very term
# SHIELD_TERM then failed to find, and the gate silently stopped seeing its own worst case.
MARKUP = re.compile(r"</?c>|</?para>|</?b>|<see\s+cref=|<paramref\s+name=|/>|[`*_\"<>]")

# The refutation routinely wraps two lines past the claim (BREAKWATER.md puts it three lines and
# a blank line later), so EXCULPATING gets the wider window. The SHIELD term gets a tight one:
# at +/-2 an unrelated "therefore shielded" sentence two lines below a TRUE heart-collider line
# was enough to make it fire (Docs/ECOSYSTEM.md 7482).
EXC_WINDOW = 3
SHIELD_WINDOW = 1


def iter_files():
    for d in SCAN_DIRS:
        base = os.path.join(ROOT, d)
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [x for x in dirnames if x not in (".git", "Library", "obj", "bin")]
            for fn in filenames:
                if fn.endswith(SCAN_EXTS):
                    yield os.path.join(dirpath, fn)
    for fn in SCAN_FILES:
        p = os.path.join(ROOT, fn)
        if os.path.exists(p):
            yield p


def scan_text(lines):
    """Return [(line_no, line)] for every unexculpated claim. 1-indexed."""
    flat = [MARKUP.sub(" ", x) for x in lines]
    out = []
    for i, line in enumerate(flat):
        # The phrase itself can straddle a line break - Docs/ECOSYSTEM.md wrapped
        # "always-on convex | MeshColliders" and escaped the first version of this gate
        # entirely. So each line is tested joined to its successor as well as alone.
        nxt = flat[i + 1] if i + 1 < len(flat) else ""
        if not CLAIM.search(line):
            # Only the PAIR matches. If the successor matches on its own it will be reported
            # there, so stay quiet - otherwise one wrapped claim is reported twice, which is how
            # the pre-fix control went from 22 findings to 42.
            if CLAIM.search(nxt) or not CLAIM.search(line + " " + nxt):
                continue

        def win(n):
            lo, hi = max(0, i - n), min(len(flat), i + n + 1)
            return " ".join(flat[lo:hi])

        if not SHIELD_TERM.search(win(SHIELD_WINDOW)):
            continue   # a heart crystal's always-on SphereCollider is real, and is not this claim
        if EXCULPATING.search(win(EXC_WINDOW)):
            continue
        out.append((i + 1, lines[i].rstrip()))
    return out


def run_check():
    findings, scanned = [], 0
    for path in iter_files():
        if os.path.abspath(path) == os.path.abspath(__file__):
            continue
        try:
            lines = io.open(path, encoding="utf-8", errors="replace").read().splitlines()
        except OSError:
            continue
        scanned += 1
        for ln, text in scan_text(lines):
            findings.append((os.path.relpath(path, ROOT), ln, text))

    if findings:
        print("shield-collider claim check: FAIL\n")
        print("A prism shield does NOT cost a collider. It swaps the MESH and the mass; the shield")
        print("components' MeshCollider is only ever `enabled = false` (four sites, no `= true`),")
        print("and their sharedMesh writes land on the MeshFilter, so there is no convex cook either.")
        print("Rewrite each line below to price the real cost (armoured mass is never food, and")
        print("super-shielded mass is removable only by an energised blade), or state plainly that")
        print("the collider claim is retired.\n")
        for rel, ln, text in findings:
            print(f"  {rel}:{ln}: {text.strip()[:140]}")
        print(f"\n{len(findings)} claim(s) in {scanned} files scanned.")
        return 1

    print(f"shield-collider claim check: OK ({scanned} files scanned, "
          f"{len(SCAN_DIRS)} trees + {len(SCAN_FILES)} root files)")
    return 0


def run_self_test():
    """Negative control: the escaped shapes must fire, the correct prose must not."""
    must_fire = [
        # the original PrismKind.cs comment
        ["/// <see cref=\"Shielded\"/> and <see cref=\"SuperShielded\"/> swap to an",
         "/// always-on convex <c>MeshCollider</c> that collider-LOD cannot reclaim - keep them rare."],
        # the Astro League budget line
        ["- **Collider budget:** +240 always-on convex MeshColliders per peer (the engaged stellated",
         "  shield swaps off the LOD-cullable BoxCollider). Static, bounded by `edgePrismCount`."],
        # the Boneyard warning
        ["/// structure. Do NOT armour the wreckage: a shielded hulk would be both un-shootable",
         "/// cover and a few thousand permanent mesh colliders."],
        # a tooltip
        ['[Tooltip("A few tougher shielded accents (always-on convex MeshCollider - kept rare).")]'],
        # the phrase split across a line break (the Wildlife Liberation band line)
        ["shielding the bars - would swap ~9,000 LOD-cullable BoxColliders for always-on convex",
         "MeshColliders (`PrismKinds`). A steering rule bought what a shield would have cost."],
    ]
    must_not_fire = [
        # a refutation on the same line
        ["A shield swaps the mesh and the mass, never the collider, so shielded mass costs zero",
         "always-on colliders."],
        # a refutation two lines below the claim (the real wrap case)
        ["because the cairn would otherwise mint roughly 4,032 always-on convex `MeshCollider`s over",
         "a match and blow the cell's collider budget before a station was laid.",
         "",
         "**Checked against the shipped code, it would not.** `shieldMeshCollider.enabled = true`",
         "appears **nowhere in the codebase**."],
        # historical narration - two spellings, both shipped
        ["/// This comment used to claim an always-on convex <c>MeshCollider</c>; that was false."],
        ["  whole economy is shooting it. (An earlier version of this line priced the ration in",
         '  "permanent mesh colliders"; that cost does not exist.)'],
        # the survey refutation table row
        ["| A shielded prism costs an always-on collider | **REFUTED** | A shield swaps the mesh",
         "and the mass, never the collider. |"],
        # a priced-at-zero budget line
        ["- **Collider budget:** **+0**. Every lining prism keeps its LOD-cullable `BoxCollider`,",
         "  because a shield swaps the MESH and the mass, never the collider."],
        # ordinary collider prose with no claim in it
        ["/// Plain and danger prisms ride the LOD-cullable BoxCollider, bounded by radius."],
        # a HEART crystal really is one always-on collider per live lifeform - not this claim
        ["- **14 always-on colliders at every intensity** (one heart each) - that is what keeps",
         "  the collider budget flat while everything else about the field changes."],
        ["// dominated by things this fuze REJECTS: every flora heart (one always-on collider per",
         "// plant), own-domain creature hearts, and free crystal drops."],
        # a correct ZERO-shields budget line
        ["**Every prism is `PrismKind.Plain` - zero always-on mesh colliders are authored.** The",
         "active count is bounded by the collider-LOD radius; the only shield here is a MASS-5",
         "pilot's own ride armour."],
    ]

    # A claim whose PHRASE wraps across a line break must be reported ONCE, not twice - the
    # line-pair join double-counted every wrapped claim and took the pre-fix control from 22
    # findings to 42, which an operator cannot tell from 20 new sites. (Cases that carry two
    # DISTINCT claim phrases on two lines legitimately report twice, so this is its own list.)
    must_fire_exactly_once = [
        ["shielding the bars - would swap ~9,000 LOD-cullable BoxColliders for always-on convex",
         "MeshColliders (`PrismKinds`). A steering rule bought what a shield would have cost."],
        ['[Tooltip("A few tougher shielded accents (always-on convex MeshCollider - kept rare).")]'],
    ]

    bad = 0
    for i, case in enumerate(must_fire):
        if not scan_text(case):
            print(f"  SELF-TEST FAIL: must-fire case {i} did not fire: {case[0][:80]}")
            bad += 1
    for i, case in enumerate(must_fire_exactly_once):
        n = len(scan_text(case))
        if n != 1:
            print(f"  SELF-TEST FAIL: wrap case {i} reported {n} times, expected exactly 1")
            bad += 1
    for i, case in enumerate(must_not_fire):
        hits = scan_text(case)
        if hits:
            print(f"  SELF-TEST FAIL: must-NOT-fire case {i} fired on: {hits[0][1][:80]}")
            bad += 1
    total = len(must_fire) + len(must_fire_exactly_once) + len(must_not_fire)
    if bad:
        print(f"self-test: FAIL ({bad} of {total} cases wrong)")
        return 1
    print(f"self-test: OK ({len(must_fire)} must-fire, "
          f"{len(must_fire_exactly_once)} must-fire-once, {len(must_not_fire)} must-not-fire)")
    return 0


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="scan the tree (default)")
    ap.add_argument("--self-test", action="store_true", help="run the negative controls only")
    a = ap.parse_args()
    sys.exit(run_self_test() if a.self_test else run_check())
