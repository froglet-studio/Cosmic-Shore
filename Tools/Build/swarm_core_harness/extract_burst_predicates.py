#!/usr/bin/env python3
"""Round 8 (Docs/SWARM_FAUNA.md §16.1), round 11a (§19.1): lift the SHIPPED prism query predicates out of
PrismSpatialIndex.cs so the VIRTUAL-entry queries a swarm member is found through can be compared against them
by RUNNING both.

Extracts, verbatim:
  * the three Burst jobs' structs (AOESpatialQueryJob, AOEConicSweepQueryJob, AOECylinderSweepQueryJob)
    plus the data they read (PrismFlags, PrismSpatialData, AOEHit);
  * PrismSpatialIndex.ConeContains and DistanceToSegmentSq (static, pure);
  * the PREPROCESSING the shipped call sites do before handing a volume to its test - QueryCone's
    direction/tangent normalisation and ProcessExplosionConeFrame's axis normalisation and gape
    re-orthogonalisation - wrapped as static methods;
  * round 11a: the virtual-entry query shape (VirtualQueryShape) and the three virtual-id queries'
    shape construction (QuerySphereVirtualIds / QuerySegmentVirtualIds / QueryConeVirtualIds), each turned
    into a factory returning the shape it would test every live virtual entry with. QueryConeVirtualIds'
    preprocessing must be TEXTUALLY QueryCone's (checked here), so the two cannot drift silently.

and writes them into one C# file compiled against BurstShim.cs (a Unity.Mathematics/Collections stand-in
with the same arithmetic). Fails loudly if any anchor is not found, so a refactor of the source cannot
make the comparison silently test nothing.

    extract_burst_predicates.py <PrismSpatialIndex.cs> <out.cs>
"""
import re
import sys


def block(src, header_regex, what):
    m = re.search(header_regex, src)
    if not m:
        sys.exit(f"extract_burst_predicates: anchor not found for {what}: {header_regex}")
    i = src.index("{", m.end() - 1 if src[m.end() - 1] == "{" else m.end())
    depth = 0
    for j in range(i, len(src)):
        if src[j] == "{":
            depth += 1
        elif src[j] == "}":
            depth -= 1
            if depth == 0:
                return src[m.start():j + 1]
    sys.exit(f"extract_burst_predicates: unbalanced braces in {what}")


def between(src, start, end, what):
    a = src.find(start)
    if a < 0:
        sys.exit(f"extract_burst_predicates: start anchor not found for {what}: {start!r}")
    b = src.find(end, a)
    if b < 0:
        sys.exit(f"extract_burst_predicates: end anchor not found for {what}: {end!r}")
    return src[a:b]


def main():
    src = open(sys.argv[1], encoding="utf-8").read()
    parts = [
        block(src, r"public static class PrismFlags\s*", "PrismFlags"),
        block(src, r"public struct PrismSpatialData\s*", "PrismSpatialData"),
        block(src, r"public struct AOEHit\s*", "AOEHit"),
        block(src, r"public struct AOESpatialQueryJob\s*:\s*IJobParallelFor\s*", "AOESpatialQueryJob"),
        block(src, r"public struct AOEConicSweepQueryJob\s*:\s*IJobParallelFor\s*", "AOEConicSweepQueryJob"),
        block(src, r"public struct AOECylinderSweepQueryJob\s*:\s*IJobParallelFor\s*", "AOECylinderSweepQueryJob"),
    ]
    cone_contains = block(src, r"public static bool ConeContains\(", "ConeContains")
    dist_seg = block(src, r"public static float DistanceToSegmentSq\(", "DistanceToSegmentSq")

    query_cone = block(src, r"public int QueryCone\(", "QueryCone")
    cone_prep = between(query_cone, "float3 dir = direction;", "// Conservative AABB", "QueryCone preprocessing")
    cone_frame = block(src, r"public bool ProcessExplosionConeFrame\(", "ProcessExplosionConeFrame")
    slab_prep = between(cone_frame, "float3 sweepAxis =", "var job = new AOEConicSweepQueryJob", "cone slab preprocessing")
    if "math.normalizesafe" not in slab_prep or "gape -=" not in slab_prep:
        sys.exit("extract_burst_predicates: the cone slab preprocessing no longer looks like what this test models")
    if "dirLenSq" not in cone_prep or "tanHalf" not in cone_prep:
        sys.exit("extract_burst_predicates: the QueryCone preprocessing no longer looks like what this test models")

    # ── round 11a: the virtual-entry queries ──
    vshape = block(src, r"struct VirtualQueryShape\s*", "VirtualQueryShape")
    vfactories = []
    for name, sig in (("QuerySphereVirtualIds", "public static bool VSphere(float3 center, float radius, out VirtualQueryShape shape)"),
                      ("QuerySegmentVirtualIds", "public static bool VSegment(float3 a, float3 b, float radius, out VirtualQueryShape shape)"),
                      ("QueryConeVirtualIds", "public static bool VCone(float3 apex, float3 direction, float length, float halfAngleDegrees, float minRadius, out VirtualQueryShape shape)")):
        body = block(src, r"public int " + name + r"\(", name)
        inner = body[body.index("{") + 1:body.rindex("}")]
        if "results.Clear();" not in inner or "return CollectVirtual(shape," not in inner or "var shape = new VirtualQueryShape" not in inner:
            sys.exit(f"extract_burst_predicates: {name} no longer looks like what this test models")
        inner = inner.replace("results.Clear();", "shape = default;").replace("return 0;", "return false;")
        inner = inner.replace("var shape = new VirtualQueryShape", "shape = new VirtualQueryShape")
        inner = re.sub(r"return CollectVirtual\(shape,[^;]*;", "return true;", inner)
        inner = inner.replace("(float3)b", "b")
        vfactories.append(sig + " {" + inner + "}")
    vcone = block(src, r"public int QueryConeVirtualIds\(", "QueryConeVirtualIds")
    norm = lambda t: re.sub(r"\s+", " ", t).strip()
    vprep = between(vcone, "float3 dir = direction;", "float endRadius", "QueryConeVirtualIds preprocessing")
    qprep = between(query_cone, "float3 dir = direction;", "float endRadius", "QueryCone preprocessing (to endRadius)")
    if norm(vprep) != norm(qprep):
        sys.exit("extract_burst_predicates: QueryConeVirtualIds' preprocessing has drifted from QueryCone's:\n"
                 + norm(vprep) + "\n  vs\n" + norm(qprep))

    out = [
        "// GENERATED by Tools/Build/swarm_core_harness/extract_burst_predicates.py from",
        "// Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs - DO NOT EDIT. The shipped Burst predicates,",
        "// verbatim, so the virtual-entry queries a swarm member is found through can be RUN against them (test R11a).",
        "using Unity.Burst; using Unity.Collections; using Unity.Jobs; using Unity.Mathematics;",
        "namespace ShippedPrismQuery {",
    ]
    out += parts
    out.append("public static class Shipped {")
    out.append(cone_contains)
    out.append(dist_seg)
    out.append("public static bool QueryConePrep(float3 apex, float3 direction, float length, float halfAngleDegrees, float minRadius,")
    out.append("    out float3 dir, out float tanHalf, out float minR) {")
    out.append("    dir = default; tanHalf = 0; minR = 0;")
    out.append("    if (length <= 0f) return false;")
    out.append("    " + cone_prep.replace("float3 dir = direction;", "dir = direction;")
               .replace("float tanHalf =", "tanHalf =")
               .replace("if (dirLenSq < 1e-8f) return 0;", "if (dirLenSq < 1e-8f) return false;"))
    out.append("    minR = minRadius; return true; }")
    out.append("public static void ConeSlabPrep(float3 axis, float3 gapeAxis, out float3 sweepAxisOut, out float3 gapeOut) {")
    out.append("    " + slab_prep)
    out.append("    sweepAxisOut = sweepAxis; gapeOut = gape; }")
    out.append(vshape.replace("struct VirtualQueryShape", "public struct VirtualQueryShape", 1))
    out += vfactories
    out.append("}")
    out.append("}")
    text = "\n".join(out)
    # QueryCone's prep reassigns the minRadius PARAMETER; the wrapper re-reads it after, which is the same value
    open(sys.argv[2], "w", encoding="utf-8").write(text)


if __name__ == "__main__":
    main()
