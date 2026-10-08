using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The pure geometry behind a crystal's faces coming off it and MATING with a vessel hull
    /// (<c>Controller/Environment/Crystals/CRYSTAL_HULL_FUSION.md</c>).
    ///
    /// Everything here is a function of meshes and numbers, with no scene access, so the parts
    /// that decide WHAT flies and WHERE it lands can be edit-mode tested without a vessel.
    ///
    /// ── A crystal is read as PANELS and FILLER ───────────────────────────────────────────────
    /// A solid is one connected piece of the crystal's mesh (vertices welded by POSITION, then
    /// connected through shared triangles); a face is a solid's triangles joined across welded
    /// edges while nearly coplanar. Each solid's outermost face is its PANEL; the rest is filler.
    /// The charge crystal is 60 pentagonal prisms (every centroid at radius 0.9406, every
    /// half-extent 0.3660 model units, measured off ChargeCrystalExport1_7-11-25.fbx), so it reads
    /// as 60 pentagon panels - the same panel/filler split the omni morph makes.
    ///
    /// ── Patches are spread first, then panels are matched to them ────────────────────────────
    /// The contact patch is the outermost outward-facing hull vertex in the direction the crystal
    /// came from, read in the hull's NORMALISED space (each axis divided by its extent). From
    /// there the remaining patches are farthest-point sampled over the skin, so they sit at
    /// near-uniform spacing on any hull shape, and each panel is matched to a patch by an optimal
    /// assignment. Mapping each panel's direction straight onto the hull was tried first and
    /// measured against the shipped Squirrel: 9 of 60 landed on a spot another already held,
    /// crowded onto the wing tips.
    ///
    /// ── A panel's corners land on the hull's own vertices ───────────────────────────────────
    /// The panel is laid in the patch's tangent plane at the patch's size, then each corner snaps
    /// to the nearest hull vertex facing the same way - so a landed face sits on the model's real
    /// geometry, wearing its real normals.
    /// </summary>
    public static class CrystalHullFusionGeometry
    {
        /// <summary>Weld tolerance for the solid and face grouping, in the crystal mesh's own units.
        /// The charge crystal's edge-arc twin is fully unwelded (one vertex per triangle corner), so
        /// position is the only thing that joins its triangles.</summary>
        public const float WeldGrid = 1e-4f;

        /// <summary>
        /// A crystal mesh read as PANELS: every solid contributes its outermost face (the one whose
        /// normal best agrees with the solid's radial from the crystal centre) as the panel that
        /// flies; every other face of that solid is FILLER that folds into it. On the charge crystal
        /// that is 60 pentagon caps carried by 60 prisms whose side quads and inner caps are absorbed
        /// first - the same panel/filler split the omni morph makes (SQUIRREL_CRYSTAL_MORPH.md §1).
        /// </summary>
        public sealed class PanelSet
        {
            /// <summary>One panel per solid.</summary>
            public int PanelCount;
            /// <summary>The mesh's bounding-box centre.</summary>
            public Vector3 Centre;
            /// <summary>Furthest vertex from <see cref="Centre"/>, mesh units.</summary>
            public float Radius;

            /// <summary>Per panel: unit direction from <see cref="Centre"/> to its solid's centroid.</summary>
            public Vector3[] Radials;
            /// <summary>Per panel: mean of its corners, mesh space.</summary>
            public Vector3[] PanelCentroids;
            /// <summary>Per panel: the face's outward unit normal.</summary>
            public Vector3[] PanelNormals;
            /// <summary>Per panel: first entry in <see cref="Corners"/>.</summary>
            public int[] CornerStart;
            /// <summary>Per panel: number of distinct corners (5 for a pentagon).</summary>
            public int[] CornerCount;
            /// <summary>Per panel: the furthest corner from the panel centroid.</summary>
            public float[] PanelRadius;
            /// <summary>Every panel's distinct corners, flat, mesh space.</summary>
            public Vector3[] Corners;

            /// <summary>Per mesh vertex: the panel (solid) it belongs to.</summary>
            public int[] VertexPanel;
            /// <summary>Per mesh vertex: its corner within its panel, or -1 for filler.</summary>
            public int[] VertexCorner;
        }

        /// <summary>
        /// Splits a mesh into solids (welded by position, connected by triangles), groups each
        /// solid's triangles into FACES (adjacent across a welded edge and within
        /// <paramref name="coplanarDegrees"/> of each other's normal - 120 of the charge crystal's
        /// 300 side quads are non-planar by 5.2 degrees, while its sharpest real crease is 57.5, see
        /// <c>CrystalEdgeArcMeshBaker.CoplanarAngleDegrees</c>), and picks each solid's outermost
        /// face as its panel. Returns null when the mesh has no triangles.
        /// </summary>
        public static PanelSet BuildPanels(Vector3[] vertices, IReadOnlyList<int[]> submeshTriangles,
                                           float coplanarDegrees = 20f)
        {
            if (vertices == null || vertices.Length == 0 || submeshTriangles == null) return null;
            int vertexCount = vertices.Length;

            // Weld by position.
            var nodeOfKey = new Dictionary<Vector3Int, int>(vertexCount);
            var vertexNode = new int[vertexCount];
            var nodePositions = new List<Vector3>();
            for (int v = 0; v < vertexCount; v++)
            {
                var key = WeldKey(vertices[v]);
                if (!nodeOfKey.TryGetValue(key, out int node))
                {
                    node = nodeOfKey.Count;
                    nodeOfKey.Add(key, node);
                    nodePositions.Add(vertices[v]);
                }
                vertexNode[v] = node;
            }

            // Flatten the triangle list.
            var tris = new List<int>();
            foreach (var list in submeshTriangles)
                if (list != null)
                    for (int i = 0; i + 2 < list.Length; i += 3) { tris.Add(list[i]); tris.Add(list[i + 1]); tris.Add(list[i + 2]); }
            int triangleCount = tris.Count / 3;
            if (triangleCount == 0) return null;

            // Solids: connected components over welded nodes.
            var nodeParent = Identity(nodeOfKey.Count);
            for (int t = 0; t < triangleCount; t++)
            {
                int a = vertexNode[tris[3 * t]];
                Union(nodeParent, a, vertexNode[tris[3 * t + 1]]);
                Union(nodeParent, a, vertexNode[tris[3 * t + 2]]);
            }

            var solidOfRoot = new Dictionary<int, int>();
            var triangleSolid = new int[triangleCount];
            for (int t = 0; t < triangleCount; t++)
            {
                int root = Find(nodeParent, vertexNode[tris[3 * t]]);
                if (!solidOfRoot.TryGetValue(root, out int solid))
                {
                    solid = solidOfRoot.Count;
                    solidOfRoot.Add(root, solid);
                }
                triangleSolid[t] = solid;
            }
            int solidCount = solidOfRoot.Count;

            // Solid centroids (over welded nodes, so a duplicated corner is not over-weighted).
            var solidSum = new Vector3[solidCount];
            var solidNodes = new int[solidCount];
            var nodeCounted = new bool[nodeOfKey.Count];
            for (int t = 0; t < triangleCount; t++)
                for (int c = 0; c < 3; c++)
                {
                    int node = vertexNode[tris[3 * t + c]];
                    if (nodeCounted[node]) continue;
                    nodeCounted[node] = true;
                    solidSum[triangleSolid[t]] += nodePositions[node];
                    solidNodes[triangleSolid[t]]++;
                }

            // Faces: triangles joined across a shared welded edge when nearly coplanar.
            var triangleNormal = new Vector3[triangleCount];
            var triangleArea = new float[triangleCount];
            for (int t = 0; t < triangleCount; t++)
            {
                Vector3 a = vertices[tris[3 * t]];
                Vector3 cross = Vector3.Cross(vertices[tris[3 * t + 1]] - a, vertices[tris[3 * t + 2]] - a);
                triangleArea[t] = cross.magnitude * 0.5f;
                Vector3 n = cross.sqrMagnitude > 1e-20f ? cross.normalized : Vector3.zero;
                // Outward from its own solid, whatever the winding: the panel is chosen by which way
                // a face LOOKS, and an inward-wound export must not pick the inner cap.
                Vector3 triangleCentre = (a + vertices[tris[3 * t + 1]] + vertices[tris[3 * t + 2]]) / 3f;
                Vector3 solidCentre = solidSum[triangleSolid[t]] / Mathf.Max(1, solidNodes[triangleSolid[t]]);
                triangleNormal[t] = Vector3.Dot(n, triangleCentre - solidCentre) < 0f ? -n : n;
            }

            float coplanarCos = Mathf.Cos(coplanarDegrees * Mathf.Deg2Rad);
            var faceParent = Identity(triangleCount);
            var edgeOwner = new Dictionary<long, int>(triangleCount * 3);
            for (int t = 0; t < triangleCount; t++)
            {
                for (int e = 0; e < 3; e++)
                {
                    int n0 = vertexNode[tris[3 * t + e]];
                    int n1 = vertexNode[tris[3 * t + (e + 1) % 3]];
                    if (n0 == n1) continue;
                    long key = n0 < n1 ? ((long)n0 << 32) | (uint)n1 : ((long)n1 << 32) | (uint)n0;
                    if (!edgeOwner.TryGetValue(key, out int other)) { edgeOwner[key] = t; continue; }
                    if (Vector3.Dot(triangleNormal[t], triangleNormal[other]) >= coplanarCos)
                        Union(faceParent, t, other);
                }
            }

            Vector3 min = vertices[0], max = vertices[0];
            for (int v = 1; v < vertexCount; v++) { min = Vector3.Min(min, vertices[v]); max = Vector3.Max(max, vertices[v]); }
            Vector3 centre = (min + max) * 0.5f;
            float radius = 0f;
            for (int v = 0; v < vertexCount; v++) radius = Mathf.Max(radius, (vertices[v] - centre).magnitude);

            var radials = new Vector3[solidCount];
            for (int s = 0; s < solidCount; s++)
            {
                Vector3 r = solidSum[s] / Mathf.Max(1, solidNodes[s]) - centre;
                radials[s] = r.sqrMagnitude > 1e-12f ? r.normalized : Vector3.forward;
            }

            // Each face's area-weighted normal, then each solid's outermost face.
            var faceNormalSum = new Dictionary<int, Vector3>();
            for (int t = 0; t < triangleCount; t++)
            {
                int face = Find(faceParent, t);
                faceNormalSum.TryGetValue(face, out var sum);
                faceNormalSum[face] = sum + triangleNormal[t] * triangleArea[t];
            }

            var panelFace = new int[solidCount];
            var panelScore = new float[solidCount];
            for (int s = 0; s < solidCount; s++) { panelFace[s] = -1; panelScore[s] = float.MinValue; }
            for (int t = 0; t < triangleCount; t++)
            {
                int face = Find(faceParent, t);
                int solid = triangleSolid[t];
                Vector3 n = faceNormalSum[face].normalized;
                float score = Vector3.Dot(n, radials[solid]);
                if (score > panelScore[solid] || (face == panelFace[solid])) { panelScore[solid] = score; panelFace[solid] = face; }
            }

            // Corners of each panel, and which mesh vertices are panel corners.
            var cornerStart = new int[solidCount];
            var cornerCount = new int[solidCount];
            var corners = new List<Vector3>();
            var panelCentroids = new Vector3[solidCount];
            var panelNormals = new Vector3[solidCount];
            var panelRadius = new float[solidCount];
            var cornerOfNode = new Dictionary<int, int>[solidCount];
            for (int s = 0; s < solidCount; s++) cornerOfNode[s] = new Dictionary<int, int>();

            for (int t = 0; t < triangleCount; t++)
            {
                int solid = triangleSolid[t];
                if (Find(faceParent, t) != panelFace[solid]) continue;
                for (int c = 0; c < 3; c++)
                {
                    int node = vertexNode[tris[3 * t + c]];
                    if (!cornerOfNode[solid].ContainsKey(node)) cornerOfNode[solid].Add(node, cornerOfNode[solid].Count);
                }
            }

            for (int s = 0; s < solidCount; s++)
            {
                cornerStart[s] = corners.Count;
                cornerCount[s] = cornerOfNode[s].Count;
                var ordered = new Vector3[cornerCount[s]];
                foreach (var pair in cornerOfNode[s]) ordered[pair.Value] = nodePositions[pair.Key];
                Vector3 mean = Vector3.zero;
                foreach (var p in ordered) mean += p;
                mean /= Mathf.Max(1, ordered.Length);
                float r = 0f;
                foreach (var p in ordered) r = Mathf.Max(r, (p - mean).magnitude);
                corners.AddRange(ordered);
                panelCentroids[s] = mean;
                panelRadius[s] = r;
                panelNormals[s] = panelFace[s] >= 0 ? faceNormalSum[panelFace[s]].normalized : radials[s];
            }

            var vertexPanel = new int[vertexCount];
            var vertexCorner = new int[vertexCount];
            for (int v = 0; v < vertexCount; v++) vertexCorner[v] = -1;
            for (int t = 0; t < triangleCount; t++)
            {
                int solid = triangleSolid[t];
                bool isPanel = Find(faceParent, t) == panelFace[solid];
                for (int c = 0; c < 3; c++)
                {
                    int v = tris[3 * t + c];
                    vertexPanel[v] = solid;
                    if (isPanel) vertexCorner[v] = cornerOfNode[solid][vertexNode[v]];
                }
            }

            return new PanelSet
            {
                PanelCount = solidCount,
                Centre = centre,
                Radius = radius,
                Radials = radials,
                PanelCentroids = panelCentroids,
                PanelNormals = panelNormals,
                CornerStart = cornerStart,
                CornerCount = cornerCount,
                PanelRadius = panelRadius,
                Corners = corners.ToArray(),
                VertexPanel = vertexPanel,
                VertexCorner = vertexCorner,
            };
        }

        /// <summary>
        /// The mesh a fusion actually draws, built once per crystal mesh: the crystal's FILLER
        /// triangles copied verbatim (every channel), and each PANEL rebuilt as a subdivided fan so
        /// it can bend over a curved hull. A flat pentagon of three triangles cannot lie on a curved
        /// skin - measured on the Squirrel, a panel's corners sat a median 0.31 patch radii off the
        /// surface - so the landing face needs interior vertices to conform.
        ///
        /// The panel keeps the charge discharge: each sub-triangle carries the
        /// <c>CrystalEdgeArcMeshBaker</c> channel contract (TEXCOORD1 barycentric, TEXCOORD2 heights
        /// in model-radius fractions with NEGATIVE = no bolt, TEXCOORD3 edge seed), with only the
        /// segments of the panel's own outline marked as bolt edges.
        /// </summary>
        public sealed class FusionTemplate
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector3[] Bary;      // TEXCOORD1
            public Vector3[] EdgeH;     // TEXCOORD2
            public Vector3[] EdgeSeed;  // TEXCOORD3
            public int[][] SubmeshTriangles;

            /// <summary>Per vertex: its panel (solid).</summary>
            public int[] VertexPanel;
            /// <summary>Per vertex: its point within its panel's grid, or -1 for filler.</summary>
            public int[] VertexPoint;

            /// <summary>Per panel: first entry in <see cref="Points"/>.</summary>
            public int[] PointStart;
            /// <summary>Per panel: number of grid points.</summary>
            public int[] PointCount;
            /// <summary>Every panel's grid points in its own plane (x along <see cref="AxisU"/>,
            /// y along <see cref="AxisV"/>, from the panel centroid), mesh units.</summary>
            public Vector2[] Points;
            /// <summary>Per panel: the plane's axes, mesh space.</summary>
            public Vector3[] AxisU;
            public Vector3[] AxisV;
        }

        /// <summary>
        /// Builds the template. <paramref name="bary"/>/<paramref name="edgeH"/>/<paramref name="edgeSeed"/>
        /// may be null (an unbaked crystal) - the filler then carries zeros, which the charge shader
        /// reads as "no edge data" and simply draws the body.
        /// </summary>
        public static FusionTemplate BuildTemplate(PanelSet panels, Vector3[] vertices, Vector3[] normals,
                                                   Vector3[] bary, Vector3[] edgeH, Vector3[] edgeSeed,
                                                   IReadOnlyList<int[]> submeshTriangles, int subdivisions,
                                                   float modelRadius)
        {
            if (panels == null || vertices == null || submeshTriangles == null) return null;
            int n = Mathf.Max(1, subdivisions);
            modelRadius = Mathf.Max(1e-6f, modelRadius);
            bool haveNormals = normals != null && normals.Length == vertices.Length;
            bool haveEdges = bary != null && edgeH != null && edgeSeed != null
                             && bary.Length == vertices.Length && edgeH.Length == vertices.Length
                             && edgeSeed.Length == vertices.Length;

            var outV = new List<Vector3>(); var outN = new List<Vector3>();
            var outB = new List<Vector3>(); var outH = new List<Vector3>(); var outS = new List<Vector3>();
            var outPanel = new List<int>(); var outPoint = new List<int>();
            var outTris = new List<int>[submeshTriangles.Count];

            // Which submesh each panel's own triangles were in, and which way its faces wind.
            int panelCount = panels.PanelCount;
            var panelSubmesh = new int[panelCount];
            var panelWindingSign = new float[panelCount];
            for (int p = 0; p < panelCount; p++) panelWindingSign[p] = 1f;

            for (int s = 0; s < submeshTriangles.Count; s++)
            {
                outTris[s] = new List<int>();
                var tris = submeshTriangles[s];
                if (tris == null) continue;
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    int panel = panels.VertexPanel[a];
                    bool isPanel = panels.VertexCorner[a] >= 0 && panels.VertexCorner[b] >= 0 && panels.VertexCorner[c] >= 0;
                    if (isPanel)
                    {
                        panelSubmesh[panel] = s;
                        Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                        if (cross.sqrMagnitude > 1e-20f)
                            panelWindingSign[panel] = Vector3.Dot(cross, panels.PanelNormals[panel]) >= 0f ? 1f : -1f;
                        continue;
                    }

                    foreach (int v in new[] { a, b, c })
                    {
                        outTris[s].Add(outV.Count);
                        outV.Add(vertices[v]);
                        outN.Add(haveNormals ? normals[v] : panels.PanelNormals[panel]);
                        outB.Add(haveEdges ? bary[v] : Vector3.zero);
                        outH.Add(haveEdges ? edgeH[v] : Vector3.zero);
                        outS.Add(haveEdges ? edgeSeed[v] : Vector3.zero);
                        outPanel.Add(panel);
                        outPoint.Add(-1);
                    }
                }
            }

            var pointStart = new int[panelCount];
            var pointCount = new int[panelCount];
            var axisU = new Vector3[panelCount];
            var axisV = new Vector3[panelCount];
            var points = new List<Vector2>();

            for (int p = 0; p < panelCount; p++)
            {
                Vector3 centroid = panels.PanelCentroids[p];
                Vector3 normal = panels.PanelNormals[p];
                int first = panels.CornerStart[p], count = panels.CornerCount[p];

                Vector3 u = count > 0 ? panels.Corners[first] - centroid : Vector3.zero;
                u -= Vector3.Dot(u, normal) * normal;
                if (u.sqrMagnitude < 1e-12f) u = Vector3.Cross(normal, Mathf.Abs(normal.x) < 0.9f ? Vector3.right : Vector3.up);
                u.Normalize();
                Vector3 v = Vector3.Cross(normal, u);
                axisU[p] = u;
                axisV[p] = v;

                // The outline, in order round the centroid.
                var outline = new List<Vector2>(count);
                for (int k = 0; k < count; k++)
                {
                    Vector3 d = panels.Corners[first + k] - centroid;
                    outline.Add(new Vector2(Vector3.Dot(d, u), Vector3.Dot(d, v)));
                }
                outline.Sort((x, y) => Mathf.Atan2(x.y, x.x).CompareTo(Mathf.Atan2(y.y, y.x)));

                pointStart[p] = points.Count;
                var pointOf = new Dictionary<Vector2Int, int>();
                int PointAt(Vector2 q)
                {
                    var key = new Vector2Int(Mathf.RoundToInt(q.x / WeldGrid), Mathf.RoundToInt(q.y / WeldGrid));
                    if (pointOf.TryGetValue(key, out int id)) return id;
                    id = pointOf.Count;
                    pointOf.Add(key, id);
                    points.Add(q);
                    return id;
                }
                Vector3 To3(Vector2 q) => centroid + u * q.x + v * q.y;

                for (int k = 0; count >= 3 && k < count; k++)
                {
                    Vector2 c0 = Vector2.zero, c1 = outline[k], c2 = outline[(k + 1) % count];
                    Vector2 Grid(int i, int j) => c0 + (c1 - c0) * (i / (float)n) + (c2 - c0) * (j / (float)n);
                    bool OnOutline(int i, int j) => i + j == n;

                    void Emit(int i0, int j0, int i1, int j1, int i2, int j2)
                    {
                        Vector2 q0 = Grid(i0, j0), q1 = Grid(i1, j1), q2 = Grid(i2, j2);
                        Vector3 p0 = To3(q0), p1 = To3(q1), p2 = To3(q2);
                        if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), normal) * panelWindingSign[p] < 0f)
                        {
                            (q1, q2) = (q2, q1); (p1, p2) = (p2, p1);
                            (i1, i2) = (i2, i1); (j1, j2) = (j2, j1);
                        }

                        var corners3 = new[] { p0, p1, p2 };
                        bool[] onOutline = { OnOutline(i0, j0), OnOutline(i1, j1), OnOutline(i2, j2) };
                        float area2 = Vector3.Cross(p1 - p0, p2 - p0).magnitude;
                        var heights = Vector3.zero;
                        var seeds = Vector3.zero;
                        for (int e = 0; e < 3; e++)
                        {
                            int j = (e + 1) % 3, l = (e + 2) % 3;
                            float length = (corners3[l] - corners3[j]).magnitude;
                            float h = length > 1e-9f ? area2 / length / modelRadius : 0f;
                            bool bolt = onOutline[j] && onOutline[l];
                            heights[e] = bolt ? h : -Mathf.Max(h, 1e-6f);
                            seeds[e] = bolt ? Hash01(p * 131 + k * 17 + Mathf.Min(i0 + i1 + i2, 999)) : 0f;
                        }

                        int baseIndex = outV.Count;
                        var ids = new[] { PointAt(q0), PointAt(q1), PointAt(q2) };
                        for (int c = 0; c < 3; c++)
                        {
                            outV.Add(corners3[c]);
                            outN.Add(normal);
                            outB.Add(c == 0 ? Vector3.right : c == 1 ? Vector3.up : Vector3.forward);
                            outH.Add(heights);
                            outS.Add(seeds);
                            outPanel.Add(p);
                            outPoint.Add(ids[c]);
                            outTris[panelSubmesh[p]].Add(baseIndex + c);
                        }
                    }

                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n - i; j++)
                        {
                            Emit(i, j, i + 1, j, i, j + 1);
                            if (i + j + 2 <= n) Emit(i + 1, j, i + 1, j + 1, i, j + 1);
                        }
                }
                pointCount[p] = points.Count - pointStart[p];
            }

            var submeshes = new int[outTris.Length][];
            for (int s = 0; s < outTris.Length; s++) submeshes[s] = outTris[s].ToArray();

            return new FusionTemplate
            {
                Vertices = outV.ToArray(),
                Normals = outN.ToArray(),
                Bary = outB.ToArray(),
                EdgeH = outH.ToArray(),
                EdgeSeed = outS.ToArray(),
                SubmeshTriangles = submeshes,
                VertexPanel = outPanel.ToArray(),
                VertexPoint = outPoint.ToArray(),
                PointStart = pointStart,
                PointCount = pointCount,
                Points = points.ToArray(),
                AxisU = axisU,
                AxisV = axisV,
            };
        }

        static float Hash01(int x)
        {
            unchecked
            {
                uint h = (uint)x * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>
        /// Everything the layout build needs, captured on the MAIN thread as plain arrays and
        /// matrices so <see cref="BuildHullLayout"/> can run on a worker: nothing in it is a
        /// <c>UnityEngine.Object</c>.
        /// </summary>
        public sealed class HullLayoutInput
        {
            /// <summary>The hull baked in hull space (renderer position and rotation removed, scale kept).</summary>
            public Vector3[] HullVertices;
            public Vector3[] HullNormals;
            public int[] HullTriangles;
            /// <summary>Per hull vertex, the bone that carries it; null pins everything to the fallback.</summary>
            public int[] DominantBones;
            /// <summary>Index used for "no bone": the renderer's root bone or the renderer itself.</summary>
            public int FallbackBone;
            /// <summary>Hull space → world, at the bake.</summary>
            public Matrix4x4 HullToWorld;
            /// <summary>World → each bone's space, at the bake (FallbackBone included).</summary>
            public Matrix4x4[] BoneWorldToLocal;

            public PanelSet Panels;
            public FusionTemplate Template;
            public float TileFill;
            public float SurfaceLift;
        }

        /// <summary>
        /// Where every face of one crystal lands on one hull, pinned to the hull's bones - built ONCE
        /// per (hull mesh, crystal mesh) and reused by every pickup, because none of it depends on
        /// the pickup: the patches, their shape on the skin and the bones that carry them are a
        /// property of the two meshes. A pickup only chooses which face takes which patch.
        ///
        /// Point <c>k</c> of a patch is where point <c>k</c> of ANY face lands: every face of the
        /// charge crystal is the same pentagon, cut by the same template, so one landed grid per
        /// patch serves all 60 faces.
        /// </summary>
        public sealed class HullLayout
        {
            public Vector3 HullCentre;
            public Vector3 InvExtents;
            public float HullMeanRadius;
            public float PatchRadius;
            public int PatchCount;
            public int PointsPerPatch;

            /// <summary>Per patch: unit direction from the hull centre, in normalised hull space.</summary>
            public Vector3[] PatchDirection;
            public int[] PatchBone;
            public Vector3[] PatchPositionLocal;
            public Vector3[] PatchNormalLocal;

            /// <summary>Per patch × point (<c>patch * PointsPerPatch + k</c>).</summary>
            public int[] PointBone;
            public Vector3[] PointLocal;
            public Vector3[] PointNormalLocal;

            public int Projected;
            public int Unprojected;
        }

        /// <summary>
        /// Builds the <see cref="HullLayout"/>. Pure - safe on a worker thread. Returns null with a
        /// named reason when the crystal's faces are not all the same shape (one landed grid could
        /// not serve them all) or the hull is empty.
        /// </summary>
        public static HullLayout BuildHullLayout(HullLayoutInput input, out string failure)
        {
            failure = null;
            var panels = input.Panels;
            var template = input.Template;
            var hv = input.HullVertices;
            var hn = input.HullNormals;
            if (panels == null || template == null || panels.PanelCount == 0) { failure = "the crystal has no faces"; return null; }
            if (hv == null || hv.Length == 0 || input.HullTriangles == null) { failure = "the hull bake is empty"; return null; }

            int perPatch = template.PointCount[0];
            for (int p = 1; p < panels.PanelCount; p++)
                if (template.PointCount[p] != perPatch || panels.CornerCount[p] != panels.CornerCount[0])
                {
                    failure = $"its faces are not one shape (face 0 has {panels.CornerCount[0]} corners, face {p} has " +
                              $"{panels.CornerCount[p]}), so one landed grid per patch cannot serve them all";
                    return null;
                }

            Vector3 min = hv[0], max = hv[0];
            for (int v = 1; v < hv.Length; v++) { min = Vector3.Min(min, hv[v]); max = Vector3.Max(max, hv[v]); }
            Vector3 centre = (min + max) * 0.5f, extents = (max - min) * 0.5f;
            Vector3 inv = InverseExtents(extents);

            int count = panels.PanelCount;
            int stride = Mathf.Max(1, Mathf.CeilToInt(hv.Length / 4096f));
            int seed = SelectHullSpot(hv, hn, centre, extents, Vector3.up, Mathf.Cos(30f * Mathf.Deg2Rad), stride);
            if (seed < 0) { failure = "the hull has no outward-facing surface"; return null; }
            var spots = FarthestPointSpots(hv, hn, centre, seed, count, stride, out float spacing);

            float meanRadius = (extents.x + extents.y + extents.z) / 3f;
            float patchRadius = 0.5f * spacing * input.TileFill;
            if (patchRadius <= 1e-5f) patchRadius = meanRadius * 0.1f;
            float lift = input.SurfaceLift * patchRadius;
            var surface = new HullSurface(hv, hn, input.HullTriangles, patchRadius * 0.35f);

            // The canonical landed face: face 0's own grid, scaled to the patch.
            var grid = new Vector2[perPatch];
            float scale = patchRadius / Mathf.Max(1e-6f, panels.PanelRadius[0]);
            for (int k = 0; k < perPatch; k++) grid[k] = template.Points[template.PointStart[0] + k] * scale;

            var layout = new HullLayout
            {
                HullCentre = centre,
                InvExtents = inv,
                HullMeanRadius = meanRadius,
                PatchRadius = patchRadius,
                PatchCount = count,
                PointsPerPatch = perPatch,
                PatchDirection = new Vector3[count],
                PatchBone = new int[count],
                PatchPositionLocal = new Vector3[count],
                PatchNormalLocal = new Vector3[count],
                PointBone = new int[count * perPatch],
                PointLocal = new Vector3[count * perPatch],
                PointNormalLocal = new Vector3[count * perPatch],
            };

            int BoneFor(int vertex)
            {
                var dominant = input.DominantBones;
                if (dominant == null || vertex < 0 || vertex >= dominant.Length) return input.FallbackBone;
                int b = dominant[vertex];
                return b >= 0 && b < input.BoneWorldToLocal.Length && b != input.FallbackBone ? b : input.FallbackBone;
            }

            void Pin(int bone, Vector3 hullPoint, Vector3 hullNormal, out Vector3 local, out Vector3 localNormal)
            {
                Matrix4x4 toBone = input.BoneWorldToLocal[bone];
                local = toBone.MultiplyPoint3x4(input.HullToWorld.MultiplyPoint3x4(hullPoint));
                localNormal = toBone.MultiplyVector(input.HullToWorld.MultiplyVector(hullNormal)).normalized;
            }

            for (int k = 0; k < count; k++)
            {
                int spot = spots[k];
                Vector3 position = hv[spot];
                Vector3 normal = hn != null && spot < hn.Length && hn[spot].sqrMagnitude > 1e-10f
                    ? hn[spot].normalized : (position - centre).normalized;

                Vector3 q = Vector3.Scale(position - centre, inv);
                layout.PatchDirection[k] = q.sqrMagnitude > 1e-12f ? q.normalized : Vector3.up;
                layout.PatchBone[k] = BoneFor(spot);
                Pin(layout.PatchBone[k], position, normal, out layout.PatchPositionLocal[k], out layout.PatchNormalLocal[k]);

                // The face's corner 0 points along the hull's own forward where it can, so faces
                // land with a consistent twist across the hull.
                Vector3 u = Vector3.forward - Vector3.Dot(Vector3.forward, normal) * normal;
                if (u.sqrMagnitude < 1e-4f) u = Vector3.right - Vector3.Dot(Vector3.right, normal) * normal;
                u = u.normalized;
                Vector3 w = Vector3.Cross(normal, u);

                for (int j = 0; j < perPatch; j++)
                {
                    Vector3 laid = position + u * grid[j].x + w * grid[j].y;
                    int at = k * perPatch + j;
                    int bone;
                    if (surface.TryProject(laid, normal, 1.5f * patchRadius, out var sp, out var sn, out int nearest))
                    {
                        layout.Projected++;
                        bone = BoneFor(nearest);
                        Pin(bone, sp + sn * lift, sn, out layout.PointLocal[at], out layout.PointNormalLocal[at]);
                    }
                    else
                    {
                        layout.Unprojected++;
                        bone = layout.PatchBone[k];
                        Pin(bone, laid + normal * lift, normal, out layout.PointLocal[at], out layout.PointNormalLocal[at]);
                    }
                    layout.PointBone[at] = bone;
                }
            }
            return layout;
        }

        /// <summary>
        /// A hull's skin, queryable for "the closest point on the surface to here". Triangles are
        /// binned into a uniform grid by their bounding boxes, so a big low-poly triangle is found
        /// from every cell it spans - on a coarse patch the nearest VERTEX can be far from the
        /// nearest SURFACE, and a vertex index would miss exactly the flat panels that matter.
        /// </summary>
        public sealed class HullSurface
        {
            readonly Vector3[] _vertices;
            readonly Vector3[] _normals;
            readonly int[] _triangles;
            readonly Vector3[] _faceNormals;
            readonly Dictionary<Vector3Int, List<int>> _grid = new();
            readonly int[] _stamp;
            int _query;
            readonly float _cell;

            public HullSurface(Vector3[] vertices, Vector3[] normals, int[] triangles, float cellSize)
            {
                _vertices = vertices;
                _normals = normals != null && normals.Length == vertices.Length ? normals : null;
                _triangles = triangles;
                _cell = Mathf.Max(1e-4f, cellSize);

                int faces = triangles.Length / 3;
                _faceNormals = new Vector3[faces];
                _stamp = new int[faces];

                for (int f = 0; f < faces; f++)
                {
                    Vector3 a = vertices[triangles[3 * f]], b = vertices[triangles[3 * f + 1]], c = vertices[triangles[3 * f + 2]];
                    Vector3 cross = Vector3.Cross(b - a, c - a);
                    _faceNormals[f] = cross.sqrMagnitude > 1e-20f ? cross.normalized : Vector3.zero;

                    Vector3Int lo = Cell(Vector3.Min(a, Vector3.Min(b, c)));
                    Vector3Int hi = Cell(Vector3.Max(a, Vector3.Max(b, c)));
                    for (int x = lo.x; x <= hi.x; x++)
                    for (int y = lo.y; y <= hi.y; y++)
                    for (int z = lo.z; z <= hi.z; z++)
                    {
                        var key = new Vector3Int(x, y, z);
                        if (!_grid.TryGetValue(key, out var list)) _grid[key] = list = new List<int>(8);
                        list.Add(f);
                    }
                }
            }

            Vector3Int Cell(Vector3 p) => new(Mathf.FloorToInt(p.x / _cell), Mathf.FloorToInt(p.y / _cell), Mathf.FloorToInt(p.z / _cell));

            /// <summary>
            /// The closest point on the skin to <paramref name="point"/> within
            /// <paramref name="maxDistance"/>, among surface whose normal agrees with
            /// <paramref name="facing"/> (never through a thin wing to its underside), with the
            /// skin's interpolated normal there and the corner of that triangle nearest the point.
            /// </summary>
            public bool TryProject(Vector3 point, Vector3 facing, float maxDistance,
                                   out Vector3 surfacePoint, out Vector3 surfaceNormal, out int nearestVertex)
            {
                surfacePoint = point;
                surfaceNormal = facing;
                nearestVertex = -1;
                _query++;

                var centre = Cell(point);
                int maxReach = Mathf.Max(1, Mathf.CeilToInt(maxDistance / _cell));
                float bestSq = maxDistance * maxDistance;
                bool found = false;

                // Grow the searched block one ring of cells at a time. A triangle not binned into any
                // cell of a block of reach r lies at least r cells from the point, so once the best
                // hit is within that, nothing outside can beat it - measured on the Squirrel, almost
                // every point stops after the first ring. Searching the whole reach up front cost
                // 80 ms for one charge crystal's points (warm .NET, so more in the Editor), and the
                // fusion paid it while the pilot watched.
                for (int reach = 1; reach <= maxReach; reach++)
                {
                    for (int x = -reach; x <= reach; x++)
                    for (int y = -reach; y <= reach; y++)
                    for (int z = -reach; z <= reach; z++)
                    {
                        // Only the new shell of cells; the inside was searched by the smaller reach.
                        if (reach > 1 && Mathf.Abs(x) < reach && Mathf.Abs(y) < reach && Mathf.Abs(z) < reach) continue;
                        if (!_grid.TryGetValue(new Vector3Int(centre.x + x, centre.y + y, centre.z + z), out var list)) continue;
                        foreach (int f in list)
                        {
                            if (_stamp[f] == _query) continue;
                            _stamp[f] = _query;
                            if (Vector3.Dot(_faceNormals[f], facing) < 0.2f) continue;

                            int i0 = _triangles[3 * f], i1 = _triangles[3 * f + 1], i2 = _triangles[3 * f + 2];
                            Vector3 q = ClosestPointOnTriangle(point, _vertices[i0], _vertices[i1], _vertices[i2], out Vector3 bc);
                            float d = (q - point).sqrMagnitude;
                            if (d >= bestSq) continue;

                            bestSq = d;
                            found = true;
                            surfacePoint = q;
                            Vector3 n = _normals != null
                                ? _normals[i0] * bc.x + _normals[i1] * bc.y + _normals[i2] * bc.z
                                : _faceNormals[f];
                            surfaceNormal = n.sqrMagnitude > 1e-12f ? n.normalized : _faceNormals[f];
                            nearestVertex = bc.x >= bc.y && bc.x >= bc.z ? i0 : bc.y >= bc.z ? i1 : i2;
                        }
                    }

                    float covered = reach * _cell;
                    if (found && bestSq <= covered * covered) break;
                }
                return found;
            }

        }

        /// <summary>Closest point on triangle (a, b, c) to <paramref name="p"/>, with its barycentric
        /// weights (Ericson, Real-Time Collision Detection §5.1.5).</summary>
        public static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bary)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) { bary = new Vector3(1, 0, 0); return a; }

            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) { bary = new Vector3(0, 1, 0); return b; }

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                bary = new Vector3(1 - v, v, 0);
                return a + v * ab;
            }

            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) { bary = new Vector3(0, 0, 1); return c; }

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                bary = new Vector3(1 - w, 0, w);
                return a + w * ac;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                bary = new Vector3(0, 1 - w, w);
                return b + w * (c - b);
            }

            float denom = 1f / (va + vb + vc);
            float vv = vb * denom, ww = vc * denom;
            bary = new Vector3(1 - vv - ww, vv, ww);
            return a + ab * vv + ac * ww;
        }

        /// <summary>
        /// Picks the hull vertex a panel heading in <paramref name="normalisedDirection"/> lands on.
        ///
        /// The direction is in the hull's NORMALISED space (each axis divided by
        /// <paramref name="extents"/>), measured from <paramref name="centre"/>. Among vertices
        /// within the cone and facing outward, the furthest (in normalised space) wins; with none in
        /// the cone, the best-aligned outward vertex; with no outward vertex at all, the
        /// best-aligned vertex. Returns -1 only for an empty hull.
        ///
        /// <paramref name="stride"/> samples every n-th vertex — a 13k-vertex hull is far denser
        /// than 60 panels need, and the pick runs once per pickup.
        /// </summary>
        public static int SelectHullSpot(Vector3[] hullVertices, Vector3[] hullNormals, Vector3 centre,
                                         Vector3 extents, Vector3 normalisedDirection, float coneCos, int stride)
        {
            if (hullVertices == null || hullVertices.Length == 0) return -1;
            stride = Mathf.Max(1, stride);
            Vector3 dir = normalisedDirection.sqrMagnitude > 1e-12f ? normalisedDirection.normalized : Vector3.forward;
            Vector3 inv = InverseExtents(extents);
            bool haveNormals = hullNormals != null && hullNormals.Length == hullVertices.Length;

            int coneBest = -1; float coneBestRadius = float.MinValue;
            int outwardBest = -1; float outwardBestDot = float.MinValue;
            int anyBest = -1; float anyBestDot = float.MinValue;

            for (int v = 0; v < hullVertices.Length; v += stride)
            {
                Vector3 offset = hullVertices[v] - centre;
                Vector3 q = Vector3.Scale(offset, inv);
                float r = q.magnitude;
                if (r < 1e-6f) continue;

                float alignment = Vector3.Dot(q / r, dir);
                if (alignment > anyBestDot) { anyBestDot = alignment; anyBest = v; }

                bool outward = !haveNormals || Vector3.Dot(hullNormals[v], offset) > 0f;
                if (!outward) continue;

                if (alignment > outwardBestDot) { outwardBestDot = alignment; outwardBest = v; }
                if (alignment >= coneCos && r > coneBestRadius) { coneBestRadius = r; coneBest = v; }
            }

            if (coneBest >= 0) return coneBest;
            if (outwardBest >= 0) return outwardBest;
            return anyBest;
        }

        /// <summary>
        /// <paramref name="count"/> hull spots spread as evenly as the surface allows: farthest-point
        /// sampling over the outward-facing vertices, seeded at <paramref name="first"/> (the
        /// contact). Each pick is the candidate furthest from every spot already taken, so the spots
        /// cover the whole skin at near-uniform spacing however the hull is shaped - a direction
        /// map cannot promise that, and on the Squirrel it piled nine panels onto the wing tips.
        ///
        /// <paramref name="spacing"/> is the distance of the LAST pick from its nearest neighbour -
        /// the minimum gap between spots, which is what sizes a panel so neighbours meet rather than
        /// overlap. Repeats are possible only when the hull has fewer candidates than spots.
        /// </summary>
        public static int[] FarthestPointSpots(Vector3[] hullVertices, Vector3[] hullNormals, Vector3 centre,
                                               int first, int count, int stride, out float spacing)
        {
            spacing = 0f;
            if (hullVertices == null || hullVertices.Length == 0 || count <= 0) return new int[0];
            stride = Mathf.Max(1, stride);
            bool haveNormals = hullNormals != null && hullNormals.Length == hullVertices.Length;

            var candidates = new List<int>(hullVertices.Length / stride + 1);
            for (int v = 0; v < hullVertices.Length; v += stride)
                if (!haveNormals || Vector3.Dot(hullNormals[v], hullVertices[v] - centre) > 0f)
                    candidates.Add(v);
            if (candidates.Count == 0)
                for (int v = 0; v < hullVertices.Length; v += stride) candidates.Add(v);

            first = Mathf.Clamp(first, 0, hullVertices.Length - 1);
            var nearest = new float[candidates.Count];
            for (int i = 0; i < nearest.Length; i++)
                nearest[i] = (hullVertices[candidates[i]] - hullVertices[first]).sqrMagnitude;

            var spots = new int[count];
            spots[0] = first;
            float lastSq = 0f;
            for (int k = 1; k < count; k++)
            {
                int bestIndex = 0;
                for (int i = 1; i < nearest.Length; i++)
                    if (nearest[i] > nearest[bestIndex]) bestIndex = i;

                lastSq = nearest[bestIndex];
                int pick = candidates[bestIndex];
                spots[k] = pick;
                Vector3 p = hullVertices[pick];
                for (int i = 0; i < nearest.Length; i++)
                {
                    float d = (hullVertices[candidates[i]] - p).sqrMagnitude;
                    if (d < nearest[i]) nearest[i] = d;
                }
            }
            spacing = Mathf.Sqrt(lastSq);
            return spots;
        }

        /// <summary>
        /// The minimum-total-cost one-to-one assignment of rows to columns of a square
        /// <paramref name="cost"/> matrix (Hungarian algorithm, O(n³) - 60 panels is ~0.2M steps,
        /// once per pickup). Returns, per row, its column.
        ///
        /// Panels use it to take patches: a greedy best-pair-first match leaves its last few panels
        /// whatever is left, which on the Squirrel sent panels straight across the hull to the
        /// opposite side; the optimum keeps every panel within ~85 degrees of where it was heading.
        /// </summary>
        public static int[] AssignMinCost(float[,] cost)
        {
            int n = cost.GetLength(0);
            var assignment = new int[n];
            if (n == 0 || cost.GetLength(1) != n) return assignment;

            // 1-indexed potentials, e-maxx formulation.
            var u = new double[n + 1];
            var v = new double[n + 1];
            var p = new int[n + 1];
            var way = new int[n + 1];
            var minv = new double[n + 1];
            var used = new bool[n + 1];

            for (int i = 1; i <= n; i++)
            {
                p[0] = i;
                int j0 = 0;
                for (int j = 0; j <= n; j++) { minv[j] = double.PositiveInfinity; used[j] = false; }
                do
                {
                    used[j0] = true;
                    int i0 = p[j0], j1 = 0;
                    double delta = double.PositiveInfinity;
                    for (int j = 1; j <= n; j++)
                    {
                        if (used[j]) continue;
                        double current = cost[i0 - 1, j - 1] - u[i0] - v[j];
                        if (current < minv[j]) { minv[j] = current; way[j] = j0; }
                        if (minv[j] < delta) { delta = minv[j]; j1 = j; }
                    }
                    for (int j = 0; j <= n; j++)
                    {
                        if (used[j]) { u[p[j]] += delta; v[j] -= delta; }
                        else minv[j] -= delta;
                    }
                    j0 = j1;
                } while (p[j0] != 0);
                do
                {
                    int j1 = way[j0];
                    p[j0] = p[j1];
                    j0 = j1;
                } while (j0 != 0);
            }

            for (int j = 1; j <= n; j++) assignment[p[j] - 1] = j - 1;
            return assignment;
        }

        /// <summary>
        /// Where on the hull sphere a panel goes, given its direction on the CRYSTAL sphere and the
        /// pole the crystal landed at: the reflection through the plane normal to the pole. The
        /// panel that touched the hull (radial = −pole) stays at the contact point, the panel on
        /// the crystal's far side wraps to the hull's antipode, and every panel keeps its angular
        /// distance from the contact — the crystal opens like a hand closing round the hull.
        /// </summary>
        public static Vector3 WrapDirection(Vector3 crystalRadial, Vector3 pole) =>
            crystalRadial - 2f * Vector3.Dot(crystalRadial, pole) * pole;

        /// <summary>Componentwise 1/extent, guarded so a flat axis does not divide by zero.</summary>
        public static Vector3 InverseExtents(Vector3 extents) => new(
            1f / Mathf.Max(1e-4f, Mathf.Abs(extents.x)),
            1f / Mathf.Max(1e-4f, Mathf.Abs(extents.y)),
            1f / Mathf.Max(1e-4f, Mathf.Abs(extents.z)));

        static Vector3Int WeldKey(Vector3 p) => new(
            Mathf.RoundToInt(p.x / WeldGrid),
            Mathf.RoundToInt(p.y / WeldGrid),
            Mathf.RoundToInt(p.z / WeldGrid));

        static int[] Identity(int n)
        {
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            return parent;
        }

        static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        static void Union(int[] parent, int a, int b)
        {
            a = Find(parent, a);
            b = Find(parent, b);
            if (a != b) parent[a] = b;
        }
    }
}
