using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The pure geometry behind a crystal FUSING onto a vessel hull — the crystal's rigid plates
    /// lifting off it and landing flush on the hull as if they had always been part of it
    /// (<c>Controller/Environment/Crystals/CRYSTAL_HULL_FUSION.md</c>).
    ///
    /// Everything here is a function of meshes and numbers, with no scene access, so the parts
    /// that decide WHERE a plate lands can be edit-mode tested without a vessel.
    ///
    /// ── A crystal is split into PLATES, not vertices ─────────────────────────────────────────
    /// A plate is one connected solid of the crystal's mesh (vertices welded by POSITION, then
    /// connected through shared triangles). The charge crystal is exactly 60 of them — 60
    /// pentagonal prisms on one shell, every centroid at radius 0.9406 and every half-extent
    /// 0.3660 in model units (measured off ChargeCrystalExport1_7-11-25.fbx). Moving each plate
    /// RIGIDLY keeps every face planar, which is what lets the charge shader's crease-edge
    /// discharge keep running on the hull: its edge data is baked per triangle in model-radius
    /// fractions, so a rigidly moved (and uniformly scaled) plate carries its bolts with it.
    ///
    /// ── Spots are spread first, then plates are matched to them ───────────────────────────────
    /// The contact spot is the outermost outward-facing hull vertex in the direction the crystal
    /// came from, read in the hull's NORMALISED space (each axis divided by its extent). From
    /// there the remaining spots are farthest-point sampled over the skin, so they sit at
    /// near-uniform spacing on any hull shape, and each plate is matched to a spot by an optimal
    /// assignment against the direction it is wrapping toward. Mapping each plate's direction
    /// straight onto the hull was tried first and measured against the shipped Squirrel: 9 of 60
    /// plates landed on a spot another plate already held, crowded onto the wing tips.
    /// </summary>
    public static class CrystalHullFusionGeometry
    {
        /// <summary>Weld tolerance for the plate grouping, in the crystal mesh's own units. The
        /// charge crystal's edge-arc twin is fully unwelded (one vertex per triangle corner), so
        /// position is the only thing that joins a plate's faces.</summary>
        public const float WeldGrid = 1e-4f;

        /// <summary>A crystal mesh split into rigid plates, each vertex expressed in its plate's
        /// own frame (z = the plate's outward radial from the crystal centre).</summary>
        public sealed class PlateSet
        {
            public int PlateCount;
            /// <summary>Per mesh vertex: the plate it belongs to.</summary>
            public int[] VertexPlate;
            /// <summary>Per plate, mesh space.</summary>
            public Vector3[] Centroids;
            /// <summary>Per plate: unit direction from <see cref="Centre"/> to its centroid.</summary>
            public Vector3[] Radials;
            /// <summary>Per plate: the plate frame in mesh space (forward = radial).</summary>
            public Quaternion[] Frames;
            /// <summary>Per mesh vertex: position in its plate's frame.</summary>
            public Vector3[] LocalPositions;
            /// <summary>Per mesh vertex: normal in its plate's frame.</summary>
            public Vector3[] LocalNormals;
            /// <summary>The mesh's bounding-box centre.</summary>
            public Vector3 Centre;
            /// <summary>Furthest vertex from <see cref="Centre"/>, mesh units.</summary>
            public float Radius;
            /// <summary>Mean, over plates, of the furthest in-plane (xy) vertex from the plate
            /// centroid — the radius of the footprint a plate lays on the hull.</summary>
            public float FootprintRadius;
            /// <summary>Mean, over plates, of the plate's extent along its own radial.</summary>
            public float Thickness;
        }

        /// <summary>
        /// Splits a mesh into its connected solids. Returns null when the mesh has no triangles.
        /// <paramref name="normals"/> may be null or short (the plate normals then default to
        /// the plate's radial).
        /// </summary>
        public static PlateSet BuildPlates(Vector3[] vertices, Vector3[] normals, IReadOnlyList<int[]> submeshTriangles)
        {
            if (vertices == null || vertices.Length == 0 || submeshTriangles == null) return null;

            int vertexCount = vertices.Length;

            // Weld by position: every vertex maps to a node, coincident vertices share one.
            var nodeOfKey = new Dictionary<Vector3Int, int>(vertexCount);
            var vertexNode = new int[vertexCount];
            for (int v = 0; v < vertexCount; v++)
            {
                var key = WeldKey(vertices[v]);
                if (!nodeOfKey.TryGetValue(key, out int node))
                {
                    node = nodeOfKey.Count;
                    nodeOfKey.Add(key, node);
                }
                vertexNode[v] = node;
            }

            var parent = new int[nodeOfKey.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;

            bool anyTriangle = false;
            foreach (var tris in submeshTriangles)
            {
                if (tris == null) continue;
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    anyTriangle = true;
                    int a = vertexNode[tris[t]];
                    Union(parent, a, vertexNode[tris[t + 1]]);
                    Union(parent, a, vertexNode[tris[t + 2]]);
                }
            }
            if (!anyTriangle) return null;

            // Roots → dense plate indices, in first-seen vertex order so the result is stable.
            var plateOfRoot = new Dictionary<int, int>();
            var vertexPlate = new int[vertexCount];
            for (int v = 0; v < vertexCount; v++)
            {
                int root = Find(parent, vertexNode[v]);
                if (!plateOfRoot.TryGetValue(root, out int plate))
                {
                    plate = plateOfRoot.Count;
                    plateOfRoot.Add(root, plate);
                }
                vertexPlate[v] = plate;
            }

            int plateCount = plateOfRoot.Count;
            Vector3 min = vertices[0], max = vertices[0];
            for (int v = 1; v < vertexCount; v++)
            {
                min = Vector3.Min(min, vertices[v]);
                max = Vector3.Max(max, vertices[v]);
            }
            Vector3 centre = (min + max) * 0.5f;

            var centroids = new Vector3[plateCount];
            var counts = new int[plateCount];
            for (int v = 0; v < vertexCount; v++)
            {
                centroids[vertexPlate[v]] += vertices[v];
                counts[vertexPlate[v]]++;
            }

            var radials = new Vector3[plateCount];
            var frames = new Quaternion[plateCount];
            for (int p = 0; p < plateCount; p++)
            {
                centroids[p] /= Mathf.Max(1, counts[p]);
                Vector3 radial = centroids[p] - centre;
                // A one-solid crystal has its only plate AT the centre - give it a direction
                // anyway so the frame is defined; it simply lands facing forward.
                radials[p] = radial.sqrMagnitude > 1e-12f ? radial.normalized : Vector3.forward;
                frames[p] = FrameFor(radials[p]);
            }

            var local = new Vector3[vertexCount];
            var localNormals = new Vector3[vertexCount];
            var footprint = new float[plateCount];
            var zMin = new float[plateCount];
            var zMax = new float[plateCount];
            for (int p = 0; p < plateCount; p++) { zMin[p] = float.MaxValue; zMax[p] = float.MinValue; }

            float radius = 0f;
            bool haveNormals = normals != null && normals.Length == vertexCount;
            for (int v = 0; v < vertexCount; v++)
            {
                int p = vertexPlate[v];
                var inverse = Quaternion.Inverse(frames[p]);
                Vector3 l = inverse * (vertices[v] - centroids[p]);
                local[v] = l;
                localNormals[v] = haveNormals ? (inverse * normals[v]).normalized : Vector3.forward;

                footprint[p] = Mathf.Max(footprint[p], new Vector2(l.x, l.y).magnitude);
                zMin[p] = Mathf.Min(zMin[p], l.z);
                zMax[p] = Mathf.Max(zMax[p], l.z);
                radius = Mathf.Max(radius, (vertices[v] - centre).magnitude);
            }

            float meanFootprint = 0f, meanThickness = 0f;
            for (int p = 0; p < plateCount; p++)
            {
                meanFootprint += footprint[p];
                meanThickness += Mathf.Max(0f, zMax[p] - zMin[p]);
            }

            return new PlateSet
            {
                PlateCount = plateCount,
                VertexPlate = vertexPlate,
                Centroids = centroids,
                Radials = radials,
                Frames = frames,
                LocalPositions = local,
                LocalNormals = localNormals,
                Centre = centre,
                Radius = radius,
                FootprintRadius = meanFootprint / plateCount,
                Thickness = meanThickness / plateCount,
            };
        }

        /// <summary>A plate frame whose forward is <paramref name="radial"/>. The up hint only
        /// fixes the twist, which nothing downstream depends on.</summary>
        public static Quaternion FrameFor(Vector3 radial)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(radial, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;
            return Quaternion.LookRotation(radial, up);
        }

        /// <summary>
        /// Picks the hull vertex a plate heading in <paramref name="normalisedDirection"/> lands on.
        ///
        /// The direction is in the hull's NORMALISED space (each axis divided by
        /// <paramref name="extents"/>), measured from <paramref name="centre"/>. Among vertices
        /// within the cone and facing outward, the furthest (in normalised space) wins; with none in
        /// the cone, the best-aligned outward vertex; with no outward vertex at all, the
        /// best-aligned vertex. Returns -1 only for an empty hull.
        ///
        /// <paramref name="stride"/> samples every n-th vertex — a 13k-vertex hull is far denser
        /// than 60 plates need, and the pick runs once per pickup.
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
        /// map cannot promise that, and on the Squirrel it piled ten plates onto the wing tips.
        ///
        /// <paramref name="spacing"/> is the distance of the LAST pick from its nearest neighbour -
        /// the minimum gap between spots, which is what sizes a plate so neighbours meet rather than
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
        /// <paramref name="cost"/> matrix (Hungarian algorithm, O(n³) - 60 plates is ~0.2M steps,
        /// once per pickup). Returns, per row, its column.
        ///
        /// Plates use it to take spots: a greedy best-pair-first match leaves its last few plates
        /// whatever is left, which on the Squirrel sent plates straight across the hull to the
        /// opposite side; the optimum keeps every plate within ~85 degrees of where it was heading.
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
        /// Where on the hull sphere a plate goes, given its direction on the CRYSTAL sphere and the
        /// pole the crystal landed at: the reflection through the plane normal to the pole. The
        /// plate that touched the hull (radial = −pole) stays at the contact point, the plate on
        /// the crystal's far side wraps to the hull's antipode, and every plate keeps its angular
        /// distance from the contact — the crystal opens like a hand closing round the hull.
        /// </summary>
        public static Vector3 WrapDirection(Vector3 crystalRadial, Vector3 pole) =>
            crystalRadial - 2f * Vector3.Dot(crystalRadial, pole) * pole;

        /// <summary>
        /// Rotates unit vector <paramref name="from"/> toward <paramref name="to"/> along their
        /// great circle. Unlike <see cref="Vector3.Slerp"/> the antipodal case is not arbitrary: it
        /// turns about an axis built from <paramref name="axisHint"/>, so a plate starting exactly
        /// opposite its target still takes a path the caller chose.
        /// </summary>
        public static Vector3 SlerpDirection(Vector3 from, Vector3 to, float t, Vector3 axisHint)
        {
            float dot = Mathf.Clamp(Vector3.Dot(from, to), -1f, 1f);
            float angle = Mathf.Acos(dot);
            if (angle < 1e-5f) return to;

            Vector3 axis = Vector3.Cross(from, to);
            if (axis.sqrMagnitude < 1e-10f)
            {
                axis = Vector3.Cross(from, axisHint);
                if (axis.sqrMagnitude < 1e-10f) axis = Vector3.Cross(from, Mathf.Abs(from.x) < 0.9f ? Vector3.right : Vector3.up);
            }
            return Quaternion.AngleAxis(angle * Mathf.Rad2Deg * Mathf.Clamp01(t), axis.normalized) * from;
        }

        /// <summary>Componentwise 1/extent, guarded so a flat axis does not divide by zero.</summary>
        public static Vector3 InverseExtents(Vector3 extents) => new(
            1f / Mathf.Max(1e-4f, Mathf.Abs(extents.x)),
            1f / Mathf.Max(1e-4f, Mathf.Abs(extents.y)),
            1f / Mathf.Max(1e-4f, Mathf.Abs(extents.z)));

        static Vector3Int WeldKey(Vector3 p) => new(
            Mathf.RoundToInt(p.x / WeldGrid),
            Mathf.RoundToInt(p.y / WeldGrid),
            Mathf.RoundToInt(p.z / WeldGrid));

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
