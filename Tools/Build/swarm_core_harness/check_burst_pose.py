#!/usr/bin/env python3
"""Round 11a-2 (Docs/SWARM_FAUNA.md §19.4): the swarm body pose is ONE static function, SwarmBodyPose.PoseMatrix
(+ the Bloom it calls), run by the harness (R11d) AND Burst-compiled by SwarmPoseJob in the game. Burst cannot be run
here, so this is the textual gate that the function stays inside what Burst compiles: scalar float maths, field reads,
KernelMath (never MathF, whose internal calls Burst cannot find) - no System.Numerics method or operator, no System.Math, no allocation, no managed construct.

Negative control (--self-test): the round-11a System.Numerics pose kept in TickJobHarness.cs (ReferenceMatrix) must FAIL.

    check_burst_pose.py <SwarmPrismSync.cs> [<TickJobHarness.cs> for --self-test]
"""
import re
import sys

FORBIDDEN = [
    (r"\bVector3\.\w+\(", "a System.Numerics.Vector3 method (Lerp/Cross/Dot/Normalize...)"),
    (r"\.Length\(\)", "Vector3.Length()"),
    (r"\bnew\s+\w", "an allocation or a constructor call"),
    (r"(?<![\w.])Math\.", "System.Math (use KernelMath or a comparison)"),
    (r"\bMathF\.", "System.MathF - Burst cannot find its internal calls ('Unable to find internal function "
                   "System.MathF::Sqrt'); use KernelMath (Unity.Mathematics in Unity, MathF in the harness)"),
    (r"\bstring\b|\bobject\b|\bclass\b", "a managed type"),
    (r"\btry\b|\bthrow\b|\bforeach\b|\?\.", "a managed construct"),
    (r"\w+\s*\[\s*\]", "a managed array"),
    (r"(?<![\w.])(?:bx|by|bz|p|face|c[0-3])\s*[\*\+\-/]\s*\(", "a Vector3 operator expression"),
]


def body(src, signature, what):
    m = re.search(signature, src)
    if not m:
        sys.exit(f"check_burst_pose: {what} not found ({signature})")
    i = src.index("{", m.end())
    depth = 0
    for j in range(i, len(src)):
        if src[j] == "{":
            depth += 1
        elif src[j] == "}":
            depth -= 1
            if depth == 0:
                return src[i:j + 1]
    sys.exit(f"check_burst_pose: unbalanced braces in {what}")


def violations(text):
    text = re.sub(r"//[^\n]*", "", text)
    return [why for pat, why in FORBIDDEN if re.search(pat, text)]


def main():
    src = open(sys.argv[1], encoding="utf-8").read()
    bad = 0
    for sig, what in ((r"public static void PoseMatrix\(", "PoseMatrix"), (r"public static float Bloom\(", "Bloom")):
        v = violations(body(src, sig, what))
        for why in v:
            print(f"  FAIL {what}: {why} - Burst (SwarmPoseJob) cannot compile it")
        bad += len(v)
    if len(sys.argv) > 2:
        ref = open(sys.argv[2], encoding="utf-8").read()
        v = violations(body(ref, r"static void ReferenceMatrix\(", "ReferenceMatrix"))
        print(f"  negative control: the round-11a System.Numerics pose trips {len(v)} rule(s)")
        if not v:
            print("  FAIL negative control: the gate passes a pose Burst could not compile")
            bad += 1
    print("burst pose gate: OK" if bad == 0 else f"burst pose gate: {bad} FAILED")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
