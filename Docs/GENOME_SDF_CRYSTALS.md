# Genome SDF crystals

An offline tool that turns tiny SDF operator graphs (the Amoebius genome lab's node format, 5-80 bytes a shape)
into faceted Unity meshes at three LODs. A prototype, 2026-11-10: **nothing uses these meshes yet**. They are not in
any prefab, scene or spawn profile.

    python3 Tools/Build/author_sdf_crystal_meshes.py              # write the meshes, draw a contact sheet in the temp dir
    python3 Tools/Build/author_sdf_crystal_meshes.py --check      # rebuild in memory, exit 1 on drift
    python3 Tools/Build/author_sdf_crystal_meshes.py --self-test  # prove the checks fire (8 negative controls)
    python3 Tools/Build/author_sdf_crystal_meshes.py --report     # print the numbers only
    python3 Tools/Build/author_sdf_crystal_meshes.py --new-guids  # three unused guids for a new spec

- Specs: `Tools/Build/sdf_crystals/specs/*.json`
- Output: `Assets/_Models/GenomeCrystals/<Name>_LOD{0,1,2}.asset`. Split vertices, one normal per facet, UV0 from a box
  projection, no tangents (32 bytes a vertex), 16-bit indices.
- Contact sheet: `--sheet <png>` (default: the temp dir; never inside the repo). Back faces are drawn red with a
  z-buffer, so red means a hole or a flipped triangle.

## The rule: the shipped crystals stay

The elemental crystals (`Crystal.fbx`, the Charge/Mass/Space/Time/Omni exports, `masscrystal.fbx`) are **not
replaced**. They carry authored motion (shape keys and animation curves) and locked looks. This tool is for **new
static kinds**: crystal and flora props, rock and shard variety, set dressing. It follows the locked "nothing in the
world looks flat" rule (`Docs/claude/SHADERS_AND_PERFORMANCE.md`): every output is a closed, faceted, lit volume.

## How a shape is meshed

1. **Exact pieces.** If the graph only uses `sdf.octa`, `sdf.box` (round 0) and `sdf.plane`, combined by sharp
   (k <= 0.0011) `intersect` / `union`, `translate` / `rotate` / `scale`, `polar` and `mirror`, the shape is a union of
   convex polytopes. Each is cut exactly with scipy's half-space intersection. Polar and mirror become copies: the
   child is first clipped to the fold's wedge or half-space, so a child that crosses the fold is still exact.
   `--self-test` checks this against the SDF's own sign at 20,000 random points per spec (agreement >= 99.9%).
   Pieces inside other pieces, and faces buried inside the union, are dropped. LOD1 and LOD2 drop each piece's
   least useful planes (volume may grow 2% / 6%) and pieces smaller than 5% / 12% of the largest one.
2. **Marching cubes.** Anything else (gyroid, sphere, torus, cylinder, smooth unions, subtract, twist, displace,
   shell) sends the whole graph to marching cubes (scikit-image) on a `res`^3 grid inside `bounds`. Then
   fast_simplification decimates it to each LOD's `lod_tris`. The result has to be closed and wound outward:
   every directed edge is matched by its reverse and the signed volume is positive.

When an LOD comes out with the same geometry as the LOD above it, its file is not written. Point a LODGroup at the
previous LOD instead (`OctaGem` and `RingCrown` have one file each).

## Measured (this container, 2026-11-10)

| spec | path | tris LOD0 / 1 / 2 | committed bytes | build ms (3 LODs) |
|---|---|---|---|---|
| GenomeOctaGem | exact, 1 piece | 8 / = / = | 4,761 | 40 |
| GenomeOctaShard | exact, octa ∩ box | 32 / 32 / 24 | 20,424 | 230 |
| GenomePolarCluster | exact, 6 pieces (polar 5) | 192 / 192 / 144 | 106,435 | 2,000 |
| GenomeKifsOcta | exact, 64 → 56 / 40 / 40 pieces | 362 / 208 / 208 | 167,144 | 14,500 |
| GenomeRingCrown | exact, 48 → 46 pieces | 552 / = / = | 80,425 | 5,000 |
| GenomeGyroidOrb | marching cubes (58,932 raw tris) | 700 / 450 / 300 | 305,380 | 130 |

That is 687 KB committed in all (Unity YAML with hex buffers; about 2.1x the raw binary). For comparison, the game's
crystal FBXs: `Crystal.fbx` 92 tris / 21 KB, `masscrystal.fbx` 60 tris / 14.6 KB, `SpaceCrystalExport1` 720 tris /
49 KB, `OmniCrystalExport1` 1,432 tris / 74 KB, `TimeCrystalExport` 360 tris / 1.4 MB (animation curves). The gem and
the shard come in under the shipped crystal. A single convex shape takes about 0.15 ms in scipy; most of the build time
above goes on the LOD plane search, and it all runs offline.

What the numbers taught:
- The gyroid needs enough triangles per tunnel to keep it open, not a fixed count. The mesh study's denser gyroid
  (scale 1.6) needed about 800 tris and self-intersected below that. Opened up to scale 1.1 / thick 0.5, the tunnels
  stay open at 300.
- Thin shells (`sdf.shell`) break decimation. Raise `res` and the LOD targets for them, or avoid them.
- Exact unions keep overlapping hulls. Buried faces are removed but T-junctions stay. That is harmless for an opaque,
  faceted material, and the reason those meshes are not checked for edge-closedness (each piece is a closed hull).

## Adding a spec

1. Write `Tools/Build/sdf_crystals/specs/<PascalName>.json`:
   `{"name", "about", "source", "guids": {"lod0", "lod1", "lod2"}, "nodes": [{"id", "op", "in", "values"}]}`,
   plus `"bounds"`, `"res"` and `"lod_tris"` when it takes the marching-cubes path. Node ids run 0..n-1. Inputs name
   earlier nodes. The last node is the root (or set `"root"`). Values are the lab's decoded values, as
   `seeds/sdf4k/*.json` stores them. They are used as written, not quantised to bytes. `sdf.spin`, `sdf.bob` and
   `sdf.material` pass through (time 0, no material).
2. `--new-guids` prints three guids that `git grep` does not find. Every run also checks that no file outside
   `Assets/_Models/GenomeCrystals/` carries one of them.
3. Run the tool, look at the contact sheet, then run `--check` and `--self-test`. Commit the spec and its assets
   together.

## Relation to the runtime generators

`Assets/_Scripts/Utility/Foundation/` builds meshes at runtime: `OctahedronMeshGenerator` and
`StellatedOctahedronMeshGenerator` (prism shield morphs sized from a box collider), `IcosphereMeshGenerator` (flat
shaded, the Astro League ball). They build ONE parametric shape at runtime from live sizes. This tool bakes arbitrary
operator graphs once, offline, into assets with no runtime cost. Use the runtime generators for a shape that has to
match a live collider, and this tool for a new fixed prop.

## Dependencies (flagged)

These are offline only and nothing in the game needs them: numpy, scipy (>= 1.10), scikit-image, fast_simplification,
Pillow. See `Tools/Build/sdf_crystals/requirements.txt` (`pip install -r`). Measured here: scikit-image 0.26.0,
scipy 1.18.1, fast_simplification 0.2.0. trimesh (5.2.0, installed) is not used.

**Risk:** the marching-cubes path (only `GenomeGyroidOrb` today) depends on scikit-image and fast_simplification
floats. Another machine with different library versions may produce a slightly different mesh, so `--check` reports
drift there. If it does, regenerate and commit; nothing references the assets. The exact path depends only on scipy's
qhull and HiGHS, and positions are rounded to 1e-4.
