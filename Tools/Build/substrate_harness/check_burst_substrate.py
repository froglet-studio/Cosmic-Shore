#!/usr/bin/env python3
"""Round 11c (Docs/SUBSTRATE_FAUNA.md §7): the substrate's per-agent step is ONE static function,
SubstrateKernel.StepAgent (+ the helpers it calls), run by the harness (SubstrateCore.RunAgentPass, group K's bit-match)
AND Burst-compiled by SubstrateAgentJob in the game. Burst cannot be run here, so this is the textual gate that the
kernel stays inside what Burst compiles - the swarm pose gate's rules (Tools/Build/swarm_core_harness/check_burst_pose.py)
plus: no Vector3 value in the kernel at all (no local, no operator - its fields are read and written as .X/.Y/.Z), no
`var`, no delegate/lambda, no `ref`-returning trick through a managed array. And the job itself: [BurstCompile], an
IJobParallelFor, NativeArray fields only, and it calls the kernel.

Negative control: the pre-11c managed step kept as the harness's reference (ReferenceStep.cs) must FAIL.

    check_burst_substrate.py <SubstrateKernel.cs> <SubstrateAgentJob.cs> <ReferenceStep.cs>
"""
import re
import sys

FORBIDDEN = [
    (r"\bVector3\.\w+\(", "a System.Numerics.Vector3 method (Lerp/Cross/Dot/Normalize...)"),
    (r"\.Length\(\)|\.LengthSquared\(\)", "Vector3.Length()"),
    (r"\bnew\s+\w", "an allocation or a constructor call"),
    (r"(?<![\w.])Math\.", "System.Math (use MathF or a comparison)"),
    (r"\bstring\b|\bobject\b|\bclass\b|\bdynamic\b", "a managed type"),
    (r"\btry\b|\bthrow\b|\bforeach\b|\?\.|\block\b", "a managed construct"),
    (r"\w+\s*\[\s*\]", "a managed array"),
    (r"\bVector3\b", "a Vector3 value (the kernel reads and writes .X/.Y/.Z only)"),
    (r"\bvar\b", "an inferred local (types must be spelled: scalar or span)"),
    (r"=>", "a lambda or delegate"),
    (r"\bList<|\bDictionary<|\bIEnumerable<", "a managed collection"),
    (r"\.Clear\(\)|\.Fill\(", "a span helper Burst may not inline (write the loop)"),
]

KERNEL_FUNCS = [
    (r"public static void StepAgent\(", "StepAgent"),
    (r"public static void Paint\(", "Paint"),
    (r"public static void PaintD\(", "PaintD"),
    (r"static int NearestPilot\(", "NearestPilot"),
    (r"static int NearestPrey\(", "NearestPrey"),
]


def body(src, signature, what):
    m = re.search(signature, src)
    if not m:
        sys.exit(f"check_burst_substrate: {what} not found ({signature})")
    i = src.index("{", m.end())
    depth = 0
    for j in range(i, len(src)):
        if src[j] == "{":
            depth += 1
        elif src[j] == "}":
            depth -= 1
            if depth == 0:
                return src[i:j + 1]
    sys.exit(f"check_burst_substrate: unbalanced braces in {what}")


def strip(text):
    text = re.sub(r"//[^\n]*", "", text)
    return re.sub(r'"(?:\\.|[^"\\])*"', '""', text)


def violations(text):
    text = strip(text)
    return [why for pat, why in FORBIDDEN if re.search(pat, text)]


def main():
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    kernel = open(sys.argv[1], encoding="utf-8").read()
    job = open(sys.argv[2], encoding="utf-8").read()
    ref = open(sys.argv[3], encoding="utf-8").read()
    bad = 0
    for sig, what in KERNEL_FUNCS:
        for why in violations(body(kernel, sig, what)):
            print(f"  FAIL SubstrateKernel.{what}: {why} - Burst (SubstrateAgentJob) cannot compile it")
            bad += 1
    # the one-line helpers are expression-bodied: scalar maths only
    for name in ("Clamp", "Lerp", "Len", "Hash"):
        m = re.search(r"static \w+ " + name + r"\(([^)]*)\)\s*=>([^;]*);", kernel)
        if not m:
            print(f"  FAIL SubstrateKernel.{name}: not found as a one-line scalar helper")
            bad += 1
            continue
        for pat, why in FORBIDDEN[:9]:
            if re.search(pat, strip(m.group(1) + m.group(2))):
                print(f"  FAIL SubstrateKernel.{name}: {why}")
                bad += 1
    # the data the job hands the kernel must be blittable: the pop/world structs hold scalars and SubstrateRegime only
    for st in ("SubstrateKernelPop", "SubstrateKernelWorld"):
        b = strip(body(kernel, r"public struct " + st + r"\b", st))
        for decl in re.findall(r"public\s+([\w<>\[\]]+)\s", b):
            if decl not in ("float", "int", "long", "byte", "SubstrateRegime"):
                print(f"  FAIL {st}: field of type {decl} is not blittable for a Burst job")
                bad += 1
    # the job: Burst-compiled, parallel-for, NativeArrays only, and it runs THE kernel (no second copy of the step)
    if not re.search(r"\[BurstCompile[^\]]*\]\s*public struct SubstrateAgentJob\s*:\s*IJobParallelFor", job):
        print("  FAIL SubstrateAgentJob: not a [BurstCompile] struct implementing IJobParallelFor")
        bad += 1
    jb = strip(body(job, r"public struct SubstrateAgentJob\b", "SubstrateAgentJob"))
    for decl in re.findall(r"^\s*(?:\[[^\]]+\]\s*)*public\s+(?!const\b)([\w<>.\[\]]+)\s+\w+[^(;\n]*;", jb, re.M):
        if not (decl.startswith("NativeArray<") or decl in ("SubstrateKernelPop", "SubstrateKernelWorld", "int")):
            print(f"  FAIL SubstrateAgentJob: field of type {decl} - a job may hold only NativeArrays and blittable values")
            bad += 1
    ex = body(job, r"public void Execute\(int \w+\)", "SubstrateAgentJob.Execute")
    if "SubstrateKernel.StepAgent(" not in ex:
        print("  FAIL SubstrateAgentJob.Execute: does not call SubstrateKernel.StepAgent")
        bad += 1
    for why in violations(ex):
        if "Vector3 value" in why:
            continue
        print(f"  FAIL SubstrateAgentJob.Execute: {why}")
        bad += 1
    # negative control
    v = violations(body(ref, r"public static void StepAgent\(", "SubstrateReference.StepAgent"))
    print(f"  negative control: the pre-11c managed step trips {len(v)} rule(s)")
    if not v:
        print("  FAIL negative control: the gate passes a step Burst could not compile")
        bad += 1
    print("burst substrate gate: OK" if bad == 0 else f"burst substrate gate: {bad} FAILED")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
