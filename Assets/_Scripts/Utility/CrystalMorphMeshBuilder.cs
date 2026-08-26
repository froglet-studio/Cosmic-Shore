using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Builds the mesh that carries a crystal's body from the crystal's own shape onto a set of
    /// octahedra — the geometry behind the Squirrel's omni-crystal morph
    /// (`Docs/…/SQUIRREL_CRYSTAL_MORPH.md`).
    ///
    /// The mesh is the SOURCE mesh, vertex for vertex, plus one extra attribute per vertex:
    /// TEXCOORD2 = (target position, phase). A vertex shader lerps position → target off one
    /// stamped clock, so the animation costs zero CPU per frame and — critically — at t = 0 the
    /// mesh renders EXACTLY as the crystal did, because it IS the crystal's geometry with the
    /// crystal's own normals, tangents and UVs. That identity is what makes the hand-off
    /// seamless; do not "optimise" it by re-generating a simplified cage.
    ///
    /// ── Why the face census is 1:1 ────────────────────────────────────────────────────────
    /// The omni crystal's cage is 122 disjoint solids: 90 box struts, 20 triangular prisms and
    /// 12 pentagonal prisms. Its NON-QUAD faces are therefore 20×2 + 12×2 = **64**, and eight
    /// shielded prisms show 8×8 = **64** octahedron faces. So every panel of the crystal becomes
    /// exactly one octahedron face, with nothing invented and nothing spare. The 660 quad faces
    /// (the struts, and the prisms' rims) are the leftovers: each collapses to a point inside
    /// the octahedron its own solid was assigned to, and is absorbed by it.
    /// Proven against the shipped FBX by `Tools/Build/measure_omni_crystal_morph.py`.
    ///
    /// ── Two traps this code is written around ─────────────────────────────────────────────
    /// 1. **A face is found STRUCTURALLY, never by coplanarity.** 60 of this cage's quads are
    ///    non-planar (the twist is ~5°), so a plane test cuts them in half and reports 160
    ///    triangle panels where there are 40 — measured, on the first pass. Two triangles cut
    ///    from one imported polygon reference the very same vertex INDICES and two triangles
    ///    from different polygons cannot, because the importer split those corners apart. That
    ///    holds here by measurement: every one of the 724 polygons carries a single normal
    ///    across its corners, so no polygon is split internally and none weld together.
    /// 2. **A SOLID is found by welded POSITION**, which is the opposite grouping and is what
    ///    keeps one strut's six faces travelling to the same octahedron.
    /// </summary>
    public static class CrystalMorphMeshBuilder
    {
        /// <summary>UV channel carrying (target position .xyz, phase .w). Read by CrystalMorph.hlsl.</summary>
        public const int TargetUVChannel = 2;

        /// <summary>Weld tolerance for the solid grouping, in the source mesh's own local units.</summary>
        const float WeldEpsilon = 1e-4f;

        /// <summary>One octahedron the crystal is morphing into, in the morph object's local space.</summary>
        public readonly struct OctahedronTarget
        {
            public readonly Vector3 Centre;
            /// <summary>8 faces × 3 corners, flat. Face f owns [3f, 3f+2].</summary>
            public readonly Vector3[] FaceCorners;

            public OctahedronTarget(Vector3 centre, Vector3[] faceCorners)
            {
                Centre = centre;
                FaceCorners = faceCorners;
            }

            public int FaceCount => FaceCorners.Length / 3;
        }

        // ── Source analysis, cached per mesh ──────────────────────────────────────────────
        // Positions/normals/tangents/UVs and the face partition are properties of the SOURCE
        // mesh alone, so they are computed once per session. Only the targets change per morph.
        sealed class Analysis
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector4[] Tangents;
            public Vector2[] Uv0;
            public int[] Triangles;

            /// <summary>Each face's triangle indices (into <see cref="Triangles"/>/3).</summary>
            public List<int>[] FaceTriangles;
            /// <summary>Each face's unique source vertex indices, ordered around the polygon.</summary>
            public int[][] FaceCorners;
            public Vector3[] FaceCentroid;
            /// <summary>Solid id per face.</summary>
            public int[] FaceSolid;
            /// <summary>Face indices that are NOT quads — the panels that become octahedron faces.</summary>
            public List<int> Panels;
            public List<int> Fillers;
            public Dictionary<int, List<int>> PanelsBySolid;
            public Dictionary<int, Vector3> SolidCentroid;
        }

        static readonly Dictionary<int, Analysis> s_analysis = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => s_analysis.Clear();

        /// <summary>
        /// Builds a morph mesh that starts as <paramref name="source"/> and ends as
        /// <paramref name="targets"/>. Returns null and fills <paramref name="diagnosis"/> when
        /// the census does not line up — fail loud rather than shipping a half-mapped morph.
        /// The caller owns the returned Mesh and must Destroy it.
        /// </summary>
        public static Mesh TryBuild(Mesh source, IReadOnlyList<OctahedronTarget> targets,
                                    float fillerPhase, float panelPhaseStart, float panelPhaseEnd,
                                    out string diagnosis)
        {
            diagnosis = null;
            if (source == null) { diagnosis = "source mesh is null"; return null; }
            if (targets == null || targets.Count == 0) { diagnosis = "no octahedron targets"; return null; }

            var a = Analyse(source);
            if (a == null) { diagnosis = $"'{source.name}' has no readable geometry (is Read/Write enabled?)"; return null; }

            int targetFaces = 0;
            for (int i = 0; i < targets.Count; i++) targetFaces += targets[i].FaceCount;
            if (a.Panels.Count != targetFaces)
            {
                diagnosis = $"'{source.name}' has {a.Panels.Count} panel faces but the targets need " +
                            $"{targetFaces} ({targets.Count} octahedra). The morph maps panels to faces 1:1.";
                return null;
            }

            var vertexTarget = new Vector4[a.Vertices.Length];
            Assign(a, targets, vertexTarget, fillerPhase, panelPhaseStart, panelPhaseEnd);

            return Emit(a, vertexTarget, source.name);
        }

        // ── Analysis ─────────────────────────────────────────────────────────────────────

        static Analysis Analyse(Mesh source)
        {
            if (s_analysis.TryGetValue(source.GetInstanceID(), out var cached)) return cached;

            var verts = source.vertices;
            var tris = source.triangles;
            if (verts.Length == 0 || tris.Length == 0) return null;

            var a = new Analysis
            {
                Vertices = verts,
                Normals = source.normals,
                Tangents = source.tangents,
                Uv0 = source.uv,
                Triangles = tris,
            };
            if (a.Normals == null || a.Normals.Length != verts.Length) a.Normals = null;
            if (a.Tangents == null || a.Tangents.Length != verts.Length) a.Tangents = null;
            if (a.Uv0 == null || a.Uv0.Length != verts.Length) a.Uv0 = null;

            // Faces: union-find over shared vertex INDICES (see the class doc — never by plane).
            var faceOf = new int[verts.Length];
            for (int i = 0; i < faceOf.Length; i++) faceOf[i] = i;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Union(faceOf, tris[t], tris[t + 1]);
                Union(faceOf, tris[t], tris[t + 2]);
            }

            // Solids: union-find over WELDED positions.
            var weldId = new int[verts.Length];
            var weldKeys = new Dictionary<Vector3Int, int>(verts.Length);
            for (int i = 0; i < verts.Length; i++)
            {
                var p = verts[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x / WeldEpsilon),
                                         Mathf.RoundToInt(p.y / WeldEpsilon),
                                         Mathf.RoundToInt(p.z / WeldEpsilon));
                if (!weldKeys.TryGetValue(key, out int id)) { id = weldKeys.Count; weldKeys[key] = id; }
                weldId[i] = id;
            }
            var solidOf = new int[weldKeys.Count];
            for (int i = 0; i < solidOf.Length; i++) solidOf[i] = i;
            for (int t = 0; t < tris.Length; t += 3)
            {
                Union(solidOf, weldId[tris[t]], weldId[tris[t + 1]]);
                Union(solidOf, weldId[tris[t]], weldId[tris[t + 2]]);
            }

            // Compact the face partition.
            var faceIndex = new Dictionary<int, int>();
            var faceTris = new List<List<int>>();
            var faceVerts = new List<HashSet<int>>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int root = Find(faceOf, tris[t]);
                if (!faceIndex.TryGetValue(root, out int fi))
                {
                    fi = faceTris.Count;
                    faceIndex[root] = fi;
                    faceTris.Add(new List<int>());
                    faceVerts.Add(new HashSet<int>());
                }
                faceTris[fi].Add(t / 3);
                faceVerts[fi].Add(tris[t]);
                faceVerts[fi].Add(tris[t + 1]);
                faceVerts[fi].Add(tris[t + 2]);
            }

            int faceCount = faceTris.Count;
            a.FaceTriangles = faceTris.ToArray();
            a.FaceCorners = new int[faceCount][];
            a.FaceCentroid = new Vector3[faceCount];
            a.FaceSolid = new int[faceCount];
            a.Panels = new List<int>();
            a.Fillers = new List<int>();
            a.PanelsBySolid = new Dictionary<int, List<int>>();
            a.SolidCentroid = new Dictionary<int, Vector3>();
            var solidAccum = new Dictionary<int, (Vector3 sum, int n)>();

            for (int f = 0; f < faceCount; f++)
            {
                var set = faceVerts[f];
                var corners = new int[set.Count];
                set.CopyTo(corners);

                Vector3 c = Vector3.zero;
                foreach (int v in corners) c += verts[v];
                c /= corners.Length;
                a.FaceCentroid[f] = c;

                // Order the corners around the polygon so the corner map below can walk both
                // outlines by perimeter fraction. Convex by construction (box / prism faces).
                OrderAroundCentroid(verts, corners, c, FaceNormal(verts, tris, faceTris[f][0]));
                a.FaceCorners[f] = corners;

                int solid = Find(solidOf, weldId[corners[0]]);
                a.FaceSolid[f] = solid;
                if (!solidAccum.TryGetValue(solid, out var acc)) acc = (Vector3.zero, 0);
                solidAccum[solid] = (acc.sum + c, acc.n + 1);

                if (corners.Length == 4) a.Fillers.Add(f);
                else
                {
                    a.Panels.Add(f);
                    if (!a.PanelsBySolid.TryGetValue(solid, out var list))
                        a.PanelsBySolid[solid] = list = new List<int>();
                    list.Add(f);
                }
            }
            foreach (var kv in solidAccum) a.SolidCentroid[kv.Key] = kv.Value.sum / kv.Value.n;

            s_analysis[source.GetInstanceID()] = a;
            return a;
        }

        static int Find(int[] p, int a)
        {
            while (p[a] != a) { p[a] = p[p[a]]; a = p[a]; }
            return a;
        }

        static void Union(int[] p, int a, int b)
        {
            int ra = Find(p, a), rb = Find(p, b);
            if (ra != rb) p[ra] = rb;
        }

        static Vector3 FaceNormal(Vector3[] verts, int[] tris, int triIndex)
        {
            int t = triIndex * 3;
            var n = Vector3.Cross(verts[tris[t + 1]] - verts[tris[t]], verts[tris[t + 2]] - verts[tris[t]]);
            return n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.up;
        }

        static void OrderAroundCentroid(Vector3[] verts, int[] corners, Vector3 centre, Vector3 normal)
        {
            if (corners.Length < 3) return;
            Vector3 u = Vector3.Cross(normal, verts[corners[0]] - centre);
            u = u.sqrMagnitude > 1e-20f ? Vector3.Cross(u, normal).normalized : Vector3.right;
            Vector3 v = Vector3.Cross(normal, u);

            var keys = new float[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 d = verts[corners[i]] - centre;
                keys[i] = Mathf.Atan2(Vector3.Dot(d, v), Vector3.Dot(d, u));
            }
            System.Array.Sort(keys, corners);
        }

        // ── Assignment ───────────────────────────────────────────────────────────────────

        static void Assign(Analysis a, IReadOnlyList<OctahedronTarget> targets, Vector4[] vertexTarget,
                           float fillerPhase, float panelPhaseStart, float panelPhaseEnd)
        {
            int octCount = targets.Count;
            var octDir = new Vector3[octCount];
            for (int k = 0; k < octCount; k++) octDir[k] = SafeDir(targets[k].Centre);

            // Panel-carrying solids spread EVENLY over the octahedra: each solid contributes the
            // same number of panels, so an even split of solids is what makes an even split of
            // faces. A solid's parts always travel together — that is the whole reason the solid
            // grouping exists.
            var panelSolids = new List<int>(a.PanelsBySolid.Keys);
            panelSolids.Sort();
            int perOct = Mathf.Max(1, panelSolids.Count / Mathf.Max(1, octCount));

            var scored = new List<(float score, int solid, int oct)>(panelSolids.Count * octCount);
            foreach (int s in panelSolids)
            {
                Vector3 d = SafeDir(a.SolidCentroid[s]);
                for (int k = 0; k < octCount; k++)
                    scored.Add((-Vector3.Dot(d, octDir[k]), s, k));
            }
            scored.Sort((x, y) => x.score != y.score ? x.score.CompareTo(y.score)
                                                     : (x.solid != y.solid ? x.solid.CompareTo(y.solid)
                                                                           : x.oct.CompareTo(y.oct)));
            var solidOct = new Dictionary<int, int>(panelSolids.Count);
            var counts = new int[octCount];
            foreach (var (_, s, k) in scored)
            {
                if (solidOct.ContainsKey(s) || counts[k] >= perOct) continue;
                solidOct[s] = k; counts[k]++;
            }
            // Anything the balanced pass could not seat (a census that is not an exact multiple)
            // falls back to nearest-octahedron so no panel is ever left without a face.
            foreach (int s in panelSolids)
                if (!solidOct.ContainsKey(s)) solidOct[s] = NearestOct(a.SolidCentroid[s], octDir);

            // Panels → the faces of THEIR solid's octahedron, greedy by angular fit.
            var faceTaken = new HashSet<(int oct, int face)>();
            var panelPairs = new List<(float score, int panel, int oct, int face)>();
            foreach (var kv in a.PanelsBySolid)
            {
                int k = solidOct[kv.Key];
                foreach (int f in kv.Value)
                {
                    Vector3 pd = SafeDir(a.FaceCentroid[f]);
                    for (int fi = 0; fi < targets[k].FaceCount; fi++)
                    {
                        Vector3 fc = FaceCentre(targets[k], fi);
                        panelPairs.Add((-Vector3.Dot(pd, SafeDir(fc - targets[k].Centre)), f, k, fi));
                    }
                }
            }
            panelPairs.Sort((x, y) => x.score != y.score ? x.score.CompareTo(y.score)
                                                         : (x.panel != y.panel ? x.panel.CompareTo(y.panel)
                                                                               : x.face.CompareTo(y.face)));
            var panelFace = new Dictionary<int, (int oct, int face)>(a.Panels.Count);
            foreach (var (_, panel, oct, face) in panelPairs)
            {
                if (panelFace.ContainsKey(panel) || faceTaken.Contains((oct, face))) continue;
                panelFace[panel] = (oct, face);
                faceTaken.Add((oct, face));
            }

            // Write the per-vertex targets.
            float phaseSpan = panelPhaseEnd - panelPhaseStart;
            foreach (var kv in panelFace)
            {
                int f = kv.Key;
                var (oct, face) = kv.Value;
                float phase = targets[oct].FaceCount > 1
                    ? panelPhaseStart + phaseSpan * (face / (float)(targets[oct].FaceCount - 1))
                    : panelPhaseStart;
                MapPanel(a, f, targets[oct], face, phase, vertexTarget);
            }

            foreach (int f in a.Fillers)
            {
                int solid = a.FaceSolid[f];
                int k = solidOct.TryGetValue(solid, out int assigned)
                    ? assigned
                    : NearestOct(a.SolidCentroid[solid], octDir);
                // Collapse to the octahedron's centre: the quad becomes a point INSIDE the
                // shield, so it is absorbed rather than left hanging as an unused face.
                var target = new Vector4(targets[k].Centre.x, targets[k].Centre.y, targets[k].Centre.z, fillerPhase);
                foreach (int tri in a.FaceTriangles[f])
                    for (int c = 0; c < 3; c++)
                        vertexTarget[a.Triangles[tri * 3 + c]] = target;
            }
        }

        static int NearestOct(Vector3 from, Vector3[] octDir)
        {
            Vector3 d = SafeDir(from);
            int best = 0; float bestDot = float.NegativeInfinity;
            for (int k = 0; k < octDir.Length; k++)
            {
                float s = Vector3.Dot(d, octDir[k]);
                if (s > bestDot) { bestDot = s; best = k; }
            }
            return best;
        }

        static Vector3 SafeDir(Vector3 v) => v.sqrMagnitude > 1e-12f ? v.normalized : Vector3.forward;

        static Vector3 FaceCentre(in OctahedronTarget t, int face) =>
            (t.FaceCorners[face * 3] + t.FaceCorners[face * 3 + 1] + t.FaceCorners[face * 3 + 2]) / 3f;

        /// <summary>
        /// Maps one panel's corners onto its target triangle by ANCHORING three of them to the
        /// target's three corners and sliding the rest along the edges between them.
        ///
        /// Anchoring is what makes a panel BECOME its face rather than sit inside it. The first
        /// version mapped by raw perimeter fraction, which is corner-to-corner only when the two
        /// polygons happen to have the same edge proportions — and an octahedron face here is
        /// 2.7 × 2.7 × 11.25, wildly unlike the cage's near-equilateral panels. Measured, that
        /// put only 83 of 336 panel corners on a target corner: every panel landed as a smaller
        /// triangle inscribed in its face, so the finished octahedra would have read as
        /// shrunken plates with gaps at the seams. Anchored, a panel's outline follows the
        /// target's outline exactly, and its area equals the face's area exactly — a pentagon's
        /// two extra corners ride ON the edges, so they add detail without changing the shape.
        ///
        /// The alignment (which source corners are the anchors, which target corner each one
        /// takes, and which way round) is chosen to move the corners least. That is ≤ 6
        /// candidates for a triangle and ≤ 60 for a pentagon — cheap, and done once per panel.
        /// </summary>
        static void MapPanel(Analysis a, int face, in OctahedronTarget target, int targetFace,
                             float phase, Vector4[] vertexTarget)
        {
            var corners = a.FaceCorners[face];
            int n = corners.Length;

            var dst = new Vector3[3];
            for (int i = 0; i < 3; i++) dst[i] = target.FaceCorners[targetFace * 3 + i];
            Vector3 dstCentre = (dst[0] + dst[1] + dst[2]) / 3f;
            Vector3 srcCentre = a.FaceCentroid[face];

            // Arc length from corner i to corner i+1, around the source polygon.
            var edge = new float[n];
            for (int i = 0; i < n; i++)
                edge[i] = (a.Vertices[corners[(i + 1) % n]] - a.Vertices[corners[i]]).magnitude;

            var mapped = new Vector3[n];
            var best = new Vector3[n];
            float bestScore = float.NegativeInfinity;
            bool haveBest = false;

            for (int dir = 1; dir >= -1; dir -= 2)
                for (int start = 0; start < n; start++)
                    for (int o1 = 1; o1 <= n - 2; o1++)
                        for (int o2 = o1 + 1; o2 <= n - 1; o2++)
                        {
                            ApplyAnchors(a, corners, edge, dst, n, dir, start, o1, o2, mapped);

                            float score = 0f;
                            for (int k = 0; k < n; k++)
                                score += Vector3.Dot(SafeDir(a.Vertices[corners[k]] - srcCentre),
                                                     SafeDir(mapped[k] - dstCentre));
                            if (haveBest && score <= bestScore) continue;

                            bestScore = score;
                            haveBest = true;
                            for (int k = 0; k < n; k++) best[k] = mapped[k];
                        }

            for (int k = 0; k < n; k++)
                vertexTarget[corners[k]] = new Vector4(best[k].x, best[k].y, best[k].z, phase);
        }

        /// <summary>
        /// One candidate alignment: walking the source polygon from <paramref name="start"/> in
        /// direction <paramref name="dir"/>, the corners at walk offsets 0, o1 and o2 become the
        /// target's corners 0, 1 and 2; every other corner lands on the target edge between the
        /// two anchors it sits between, at its own share of the arc length.
        /// </summary>
        static void ApplyAnchors(Analysis a, int[] corners, float[] edge, Vector3[] dst, int n,
                                 int dir, int start, int o1, int o2, Vector3[] mapped)
        {
            int Walk(int offset) => ((start + dir * offset) % n + n) % n;

            // Arc length along the walk, so a corner's position between two anchors is measured
            // in the SOURCE's own proportions rather than by index.
            var arc = new float[n + 1];
            for (int step = 0; step < n; step++)
            {
                int from = Walk(step);
                // Walking backwards traverses the edge that ENDS at `from`, not the one that starts there.
                arc[step + 1] = arc[step] + edge[dir > 0 ? from : ((from - 1) % n + n) % n];
            }

            int[] anchorAt = { 0, o1, o2, n };
            for (int seg = 0; seg < 3; seg++)
            {
                int fromStep = anchorAt[seg], toStep = anchorAt[seg + 1];
                float span = Mathf.Max(1e-6f, arc[toStep] - arc[fromStep]);
                for (int step = fromStep; step < toStep; step++)
                {
                    float u = (arc[step] - arc[fromStep]) / span;
                    mapped[Walk(step)] = Vector3.Lerp(dst[seg], dst[(seg + 1) % 3], u);
                }
            }
        }

        // ── Emit ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Emits the morph mesh with UNSHARED vertices — one per triangle corner.
        ///
        /// Sharing is what would break it: a vertex reachable from two faces has two targets and
        /// only one slot to put them in. Unsharing is visually identical (positions, normals,
        /// tangents and UVs are copied per corner, which is exactly what a hard-edged import
        /// already stores) and costs ~4.3k vertices on this cage, so it buys correctness for
        /// nothing worth measuring.
        /// </summary>
        static Mesh Emit(Analysis a, Vector4[] vertexTarget, string sourceName)
        {
            int count = a.Triangles.Length;
            var verts = new Vector3[count];
            var norms = a.Normals != null ? new Vector3[count] : null;
            var tans = a.Tangents != null ? new Vector4[count] : null;
            var uv0 = a.Uv0 != null ? new Vector2[count] : null;
            var uv2 = new Vector4[count];
            var tris = new int[count];

            for (int i = 0; i < count; i++)
            {
                int src = a.Triangles[i];
                verts[i] = a.Vertices[src];
                if (norms != null) norms[i] = a.Normals[src];
                if (tans != null) tans[i] = a.Tangents[src];
                if (uv0 != null) uv0[i] = a.Uv0[src];
                uv2[i] = vertexTarget[src];
                tris[i] = i;
            }

            var mesh = new Mesh
            {
                name = $"CrystalMorph_{sourceName}",
                // Runtime-only: never let a generated mesh serialize into a scene. DontSave
                // also exempts it from Resources.UnloadUnusedAssets, so the caller's explicit
                // Destroy (SquirrelCrystalMorph.OnDestroy) is what keeps it from accumulating.
                hideFlags = HideFlags.DontSave,
                indexFormat = count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(verts);
            if (norms != null) mesh.SetNormals(norms);
            if (tans != null) mesh.SetTangents(tans);
            if (uv0 != null) mesh.SetUVs(0, uv0);
            mesh.SetUVs(TargetUVChannel, uv2);
            mesh.SetTriangles(tris, 0, calculateBounds: true);

            // The morph SWEEPS from the crystal to the ring, so the mesh's own bounds (the
            // crystal) frustum-cull it the moment the panels leave. Encapsulate every target too
            // — the same rule any vertex-displacing animation follows.
            var b = mesh.bounds;
            for (int i = 0; i < uv2.Length; i++) b.Encapsulate(new Vector3(uv2[i].x, uv2[i].y, uv2[i].z));
            mesh.bounds = b;
            return mesh;
        }
    }
}
