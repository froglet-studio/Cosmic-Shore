#!/usr/bin/env python3
"""KernelMath (Assets/.../FloraAndFauna/Swarm/KernelMath.cs) is the scalar maths of the kernels a Burst job AND a pure-.NET
harness both run (SubstrateKernel.StepAgent, SwarmBodyPose.PoseMatrix). It has two branches: UNITY_5_3_OR_NEWER
forwards to Unity.Mathematics (what Burst maps onto its float intrinsics) and the harness branch forwards to MathF.
Burst cannot be run here and the harness only ever compiles the MathF branch, so this is the textual gate that:

  1. the Unity branch never calls MathF or System.Math (Burst 1.8 cannot find MathF's internal calls - the
     "Unable to find internal function `System.MathF::Sqrt`" error that sent both jobs back to managed code),
  2. every Unity-branch method forwards to math.<same name, lower-cased>,
  3. the two branches declare the same methods with the same parameter lists (the harness proves the one the
     game runs), and
  4. every KernelMath member the given kernels use is declared.

Negative control (always run): a copy whose Unity branch forwards to MathF, and one with a missing twin, must FAIL.

    check_kernel_math.py <KernelMath.cs> [<kernel.cs> ...]
"""
import re
import sys

METHOD = re.compile(r"public static float (\w+)\(([^)]*)\)\s*=>\s*([^;]*);")


def branches(src):
    m = re.search(r"#if UNITY_5_3_OR_NEWER\s*\n(.*?)#else\s*\n(.*?)#endif", src[src.index("class KernelMath"):], re.S)
    if not m:
        return None, None
    return m.group(1), m.group(2)


def problems(src, kernels):
    out = []
    unity, harness = branches(src)
    if unity is None:
        return ["no `#if UNITY_5_3_OR_NEWER ... #else ... #endif` method block inside class KernelMath"]
    u = {n: (a.strip(), b.strip()) for n, a, b in METHOD.findall(unity)}
    h = {n: (a.strip(), b.strip()) for n, a, b in METHOD.findall(harness)}
    if not u:
        out.append("the Unity branch declares no method")
    for name, (params, body) in u.items():
        if re.search(r"\bMathF\b|(?<![\w.])Math\.", body):
            out.append(f"Unity branch {name}: calls MathF/System.Math ({body}) - Burst cannot compile it")
        if not re.match(r"math\." + name.lower() + r"\(", body):
            out.append(f"Unity branch {name}: does not forward to math.{name.lower()} ({body})")
    for name in sorted(set(u) ^ set(h)):
        out.append(f"{name}: declared in only one branch - the harness would prove a different function than the game runs")
    for name in sorted(set(u) & set(h)):
        if u[name][0] != h[name][0]:
            out.append(f"{name}: parameter lists differ between the branches ({u[name][0]} vs {h[name][0]})")
        if not re.match(r"MathF\." + name + r"\(", h[name][1]):
            out.append(f"harness branch {name}: does not forward to MathF.{name} ({h[name][1]})")
    declared = set(u) & set(h)
    consts = set(re.findall(r"public const float (\w+)\s*=", src))
    for path, text in kernels:
        text = re.sub(r"//[^\n]*", "", text)
        for used in sorted(set(re.findall(r"\bKernelMath\.(\w+)", text))):
            if used not in declared and used not in consts:
                out.append(f"{path}: uses KernelMath.{used}, which is not declared in both branches")
    return out


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    src = open(sys.argv[1], encoding="utf-8").read()
    kernels = [(p, open(p, encoding="utf-8").read()) for p in sys.argv[2:]]
    bad = problems(src, kernels)
    for why in bad:
        print(f"  FAIL KernelMath: {why}")
    # negative controls: the gate must reject a Unity branch that went back to MathF, and a missing twin
    unity, _ = branches(src)
    controls = 0
    if unity is not None:
        regressed = src.replace(unity, unity.replace("math.sqrt(", "System.MathF.Sqrt("), 1)
        orphan = src.replace(unity, re.sub(r"[^\n]*public static float Acos\([^\n]*\n", "", unity), 1)
        for label, mutant in (("Unity branch back on MathF", regressed), ("Acos missing from the Unity branch", orphan)):
            if mutant == src or not problems(mutant, kernels):
                print(f"  FAIL negative control: the gate passes '{label}'")
                bad.append(label)
            else:
                controls += 1
    print(f"  negative controls: {controls} mutant(s) rejected")
    print("kernel math gate: OK" if not bad else f"kernel math gate: {len(bad)} FAILED")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
