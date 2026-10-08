#!/usr/bin/env python3
"""Round 11a-2 (Docs/SWARM_FAUNA.md §19.4): the swarm body pose is ONE static function, SwarmBodyPose.PoseMatrix
(+ the Bloom it calls), run by the harness (R11d) AND Burst-compiled by SwarmPoseJob in the game. Burst cannot be run
here, so this is the textual gate that the function stays inside what Burst compiles: scalar float maths, field reads,
MathF.Abs and the class's one-line (float)System.Math helpers - no other MathF member, no System.Numerics method or
operator, no inline System.Math, no allocation, no managed construct.

No MathF member but Min/Max/Abs/PI anywhere in SwarmBodyPose (2026-10-08, Docs/SWARM_FAUNA.md §19.4): MathF.Sqrt is an
InternalCall in Unity's Mono that Burst cannot link ("Unable to find internal function `System.MathF::Sqrt`" in
Editor.log), and because every Assembly-CSharp job shares one Burst library, that one call ran EVERY game job as managed
code. This gate used to recommend MathF.

Negative controls (--self-test): the round-11a System.Numerics pose kept in TickJobHarness.cs (ReferenceMatrix) must
FAIL, and the MathF rule must fire on MathF.Sqrt and pass MathF.Abs.

    check_burst_pose.py <SwarmPrismSync.cs> [<TickJobHarness.cs> for --self-test]
"""
import re
import sys

FORBIDDEN = [
    (r"\bVector3\.\w+\(", "a System.Numerics.Vector3 method (Lerp/Cross/Dot/Normalize...)"),
    (r"\.Length\(\)", "Vector3.Length()"),
    (r"\bnew\s+\w", "an allocation or a constructor call"),
    (r"\bMathF\.(?!(?:Min|Max|Abs|PI)\b)\w+", "a System.MathF extern (an InternalCall Burst cannot link - and one turns Burst off for "
                                            "EVERY job in Assembly-CSharp; call the class's (float)System.Math helper)"),
    (r"(?<![\w.])Math\.", "System.Math inline (double maths in the pose - call the class's one-line float helper)"),
    (r"\bstring\b|\bobject\b|\bclass\b", "a managed type"),
    (r"\btry\b|\bthrow\b|\bforeach\b|\?\.", "a managed construct"),
    (r"\w+\s*\[\s*\]", "a managed array"),
    (r"(?<![\w.])(?:bx|by|bz|p|face|c[0-3])\s*[\*\+\-/]\s*\(", "a Vector3 operator expression"),
]


MATHF_RULE = 3  # index of the MathF-extern rule in FORBIDDEN


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
    # no MathF extern anywhere in the class (a helper the pose calls is not walked above), and its float wrappers over
    # System.Math (a Burst intrinsic) have exactly the `=> (float)Math.<Name>(...)` form
    pose = re.sub(r"//[^\n]*", "", body(src, r"public static class SwarmBodyPose\b", "SwarmBodyPose"))
    pat, why = FORBIDDEN[MATHF_RULE]
    for hit in sorted(set(re.findall(pat, pose))):
        print(f"  FAIL SwarmBodyPose: {hit} - {why}")
        bad += 1
    for name, rhs in re.findall(r"static float (\w+)\([^)]*\)\s*=>([^;]*);", pose):
        if "Math." in rhs and not re.fullmatch(r"\s*\(float\)Math\." + name + r"\([^;]*\)\s*", rhs):
            print(f"  FAIL SwarmBodyPose.{name}: must be `=> (float)Math.{name}(...)` (Burst intrinsic), is `{rhs.strip()}`")
            bad += 1
    if not re.search(pat, "MathF.Sqrt(x)") or re.search(pat, "MathF.Abs(a) + MathF.PI"):
        print("  FAIL negative control: the MathF rule does not separate MathF.Sqrt (extern) from MathF.Abs (IL)")
        bad += 1
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
