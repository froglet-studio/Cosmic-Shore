# Generated meshes built at load instead of shipped (prototype, 2026-11-10)

Branch `claude/genome-mesh-at-load`. An audit found about 6.5 MB of generated meshes saved as assets in the
build. The pattern proven on `claude/genome-membrane-loop` (`CapsuleMembraneLoop.cs`: run the same
deterministic function at load, share the result, test it against the shipped asset) is applied here to the
asset whose generator is a small parametric function, and judged for the rest.

Owners: the spindle LOD meshes and the crystal hull fusion both landed through Shombith's merges (#1654,
2026-10-31); `who_works_where.py query` shows no unmerged work in flight on either. Nothing shipped is
rewired on this branch, so no one's tuning changes.

## 1. The switch-ring wire: `SpindleWireRing`, `SpindleWireRingLow`, `SpindleWireRingThread`

| | |
|---|---|
| Generator | `Tools/Build/author_spindle_lod_meshes.py` `build_wire(segments, strands)` + `_emit_quad` + `compute_tangents` (from `author_primitive_meshes.py`), written by `mesh_text` |
| Inputs | constants only: ring radius 0.5, helix 0.022, strand radius 0.016, 6 sides, 24 twists, 3 strands, 4 wraps of u; 144 segments (near) or 48 (low, thread = 0 strands) |
| Consumers | ONE reference each: the synthetic `MeshSet` of `Assets/Resources/SpindleLodProfile.asset` (`SpindleLodProfileSO.SyntheticSet`), read by `ToyFactory.WireRingMaterial` / `SwitchRingLod` for the Toybox switch rings. No prefab, no scene, no MeshFilter in YAML. |
| Bytes on disk | 1,807,318 + 604,628 + 203,730 = **2,615,676** (the audit's 6.5 MB counted the first two, 2.41 MB) |
| Mesh | near 10,368 verts / 15,552 indices; low 3,456 / 5,184; thread 1,152 / 1,728. One stream, stride 84 (pos, normal, tangent, uv0.xy, uv1/2/3.xyz), uint16 |
| Verdict | **Port done. Bit-identical.** |
| Generation time | measured in the .NET harness (net8, optimised, 4 shared cores): near 13.6 ms cold (JIT) / **2.0 ms warm**; low 1.1 / 0.6 ms; thread 0.3 ms. Unity's Mono / IL2CPP not measured |

**What was built.**
- `Assets/_Scripts/Utility/SpindleWireRingGeometry.cs`: the generator, line for line, in double precision
  in the script's order of operations, narrowed to float at the end. No UnityEngine dependency.
- `Assets/_Scripts/Utility/SpindleWireRingMesh.cs`: `SpindleWireRingMesh.Get(Tier.Near | Low | Thread)`
  builds each tier once and returns the same shared `Mesh` to every caller (never destroy it); `Build(tier)`
  makes a fresh one. Same vertex layout, uint16 indices and bounds as the asset.
- `Assets/_Scripts/Tests/Editor/SpindleWireRingMeshTests.cs`: loads each shipped asset with
  `AssetDatabase.LoadAssetAtPath<Mesh>` and compares vertex count, the index buffer exactly, positions,
  normals, tangents, uv0 and bounds within 1e-4; plus a sharing test.
- `Tools/Build/genome_mesh_parity/run.sh` + `Parity.cs`: compiles the geometry file alone with the
  repo's nuget `csc.dll` root (`~/.dotnet`), parses each asset's `_typelessdata` and `m_IndexBuffer` hex,
  and diffs. Result: all three meshes **0 of 217,728 / 72,576 / 24,192 floats differ in any bit, every index
  equal**; a negative control (vertex 0 nudged by 0.001) is caught.

**How to switch (not done here).** `SpindleLodProfileSO.MeshSet` holds `source/mid/low/far` mesh
references. Smallest change: when the synthetic set's references are empty, fill them at
`OnEnable`/first use from `SpindleWireRingMesh.Get` (source and mid = Near, low = Low, far = Thread, as the
script writes them). Then blank the four references in `SpindleLodProfile.asset`, delete the three assets,
and make `author_spindle_lod_meshes.py` stop writing them (its `measured_block` writes the synthetic set's
guids, and `--check` would otherwise call their absence drift). The profile's measured bounds for the wire
(`wire_measure`) stay as they are; they describe the same mesh.

**Unverified.**
- The EditMode tests have not run in Unity (no editor here). The harness proves the algorithm on the raw
  bytes; the test proves the `Mesh` assembly (`SetVertexBufferParams` layout, bounds) only once it runs.
- Unity's `Math.Cos/Sin` on Mono and IL2CPP may differ from .NET 8 in the last ulp; the test's 1e-4
  tolerance covers that, bit-identity is only shown on .NET.
- The runtime mesh is readable (the asset was too: `m_IsReadable: 1`). Call `UploadMeshData(true)` after
  build if nobody reads it back, to free the CPU copy (about 0.9 MB for the near tier).
- `SwitchRingLod` / the LOD system have not been run with a runtime-built mesh.

## 2. The crystal hull fusion templates: `<Element>_FusionTemplate.asset`

| | |
|---|---|
| Files | `Assets/_SO_Assets/CrystalHullFusion/Charge_` 1,465,574, `Mass_` 860,359, `Space_` 1,162,752, `Time_` 583,542 = **4,072,227** bytes |
| Generator | `Assets/_Scripts/Editor/CrystalHullFusionBaker.cs` (FrogletTools > Vessels > Wiring > Bake Crystal Hull Fusions) calling `CrystalHullFusionGeometry.Solve` (1,322 lines, `Utility/`) then `CrystalHullFusion.BuildTemplateMesh` |
| Inputs | the element's crystal prefab mesh (vertices, normals, the charge discharge channels UV1-3, triangles) AND a hull mesh (bind pose, bones), plus `tileFill` / `surfaceLift` and the face subdivision |
| Consumers | 11 `<Vessel>_<Element>_HullFusionBake.asset` each per element (`CrystalHullFusionBakeSO.TemplateMesh`), read by `CrystalHullFusion` at pickup and cloned per vessel |
| Verdict | **Reproducible, but not a small port; stop here.** |

Why not now:
- It is not a parametric shape: it is the solved output of a 1,322-line geometry solver (panel detection,
  peel, Hungarian matching) run on two imported meshes. It is deterministic (the bake tests say so), and a
  runtime path already exists: the worker-thread fallback `CrystalHullFusion.Solve` the game uses for a
  missing or stale bake, measured at about 0.1 s per pair on .NET (`CRYSTAL_HULL_FUSION.md` §6).
- So the "port" is already written; what dropping the asset costs is a ~0.1 s worker solve per element per
  session plus reading the crystal's mesh, which is non-readable for Charge (`CrystalEdgeArcMeshBaker.TryGetReadable`
  re-bakes a readable twin). That cost was the reason the bake was made (the doc's "fourth cut").
- The template is said to depend on the crystal alone, but `Solve` takes the hull as well; whether a
  hull-free template solve gives the same bytes as the shipped one is unverified.
- Each mesh carries two sub-meshes (2,340 + 8,100 verts for Charge) with UV0 used as (face, point)
  bookkeeping, so a parity test would compare the solver's output rather than a formula.

If the 4 MB matters for a platform, the cheaper next step is to measure the fallback solve in a player
(Mono and IL2CPP, one phone) and, if it fits the vessel-spawn window, have `Prewarm` solve the template once
per element and keep the per-hull bakes (small) as they are. That is the crystal fusion owner's call.

## 3. Totals

| Asset | Bytes | Status |
|---|---|---|
| SpindleWireRing (+Low, +Thread) | 2,615,676 | ported, bit-identical on .NET, not yet switched |
| `*_FusionTemplate` x4 | 4,072,227 | runtime path exists (fallback solve); not switched, needs a player measurement |

Bytes are the YAML text in the repo; the build's serialized binary size is smaller and was not measured.
