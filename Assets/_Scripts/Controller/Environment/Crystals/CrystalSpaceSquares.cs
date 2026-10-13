using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Plays the Space element's SQUARES animation on the omni crystal's body (Omni Shepard Lab round 9,
    /// <c>Docs/Studios/OMNI_SHEPARD_LAB.md</c>): the 60 square plates turn about the pentagons, then weave over
    /// the triangles, a 4.8 s loop from the artist's .blend (<see cref="CrystalSpaceSquaresSO"/>).
    ///
    /// Lives on the body (<c>OmniCrystalBody.prefab</c>), whose SkinnedMeshRenderer draws a runtime twin of the
    /// FBX mesh: <see cref="RhombusSkinBaker"/> skins it for the Time flip wave, and
    /// <see cref="CrystalDepthPrisms"/> cuts the triangle holes. The FBX carries no shape keys, so the first frame
    /// the body is visible this swaps it onto a copy of whatever twin it draws with the three keys added as blend
    /// shapes (<see cref="CrystalSpaceSquaresBaker"/>; one copy per twin, shared by every omni), then drives their
    /// weights from the clock. A blend shape is applied before skinning, so the flip wave's bones still turn the
    /// rhombi on top - the squares are bound to the still root bone and are the keys' alone.
    ///
    /// Each crystal plays at its own phase, so a field of omnis does not weave in lockstep.
    /// </summary>
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class CrystalSpaceSquares : MonoBehaviour
    {
        static readonly ProfilerMarker UpdateMarker = new("CrystalSpaceSquares.LateUpdate");

        [Header("Animation")]
        [Tooltip("The squares' shape keys and frame weights. OmniSpaceSquares.asset is generated from the art file's bake (Tools/Build/author_omni_crystal_triangles.py).")]
        [SerializeField] CrystalSpaceSquaresSO squares;

        SkinnedMeshRenderer body;
        Mesh shaped;
        int[] shapeIndex;
        float[] weights;
        float phase;

        void Awake()
        {
            body = GetComponent<SkinnedMeshRenderer>();
            if (!squares || squares.FrameCount == 0)
            {
                CSDebug.LogError($"CrystalSpaceSquares on '{name}': no squares asset, or it carries no frames. The squares will hold still.", this);
                enabled = false;
                return;
            }
            shapeIndex = new int[squares.Keys.Length];
            weights = new float[squares.Keys.Length];
            // Its own stream: the global UnityEngine.Random is seeded for deterministic content.
            phase = (float)new System.Random(GetInstanceID()).NextDouble() * squares.LoopSeconds;
        }

        void LateUpdate()
        {
            using var _ = UpdateMarker.Auto();
            // A body nobody can see does no work; the weights are a pure function of the clock.
            if (!body.isVisible) return;
            if (body.sharedMesh != shaped && !TryWearShapes()) return;

            squares.WeightsAt(Time.time + phase, weights);
            for (int k = 0; k < weights.Length; k++)
                body.SetBlendShapeWeight(shapeIndex[k], weights[k] * 100f);
        }

        /// <summary>Puts the body on a twin carrying the squares' blend shapes, and finds their indices.</summary>
        bool TryWearShapes()
        {
            if (!CrystalSpaceSquaresBaker.TryGetShaped(body.sharedMesh, squares, out var mesh, out var problem))
            {
                CSDebug.LogError($"CrystalSpaceSquares on '{name}': {problem}. The squares will hold still.", this);
                enabled = false;
                return false;
            }
            if (mesh != body.sharedMesh)
            {
                // Swapping the mesh keeps the bones; the bounds are carried over explicitly so a turning rhombus
                // is still never culled.
                var bounds = body.localBounds;
                body.sharedMesh = mesh;
                body.localBounds = bounds;
            }
            for (int k = 0; k < shapeIndex.Length; k++)
                shapeIndex[k] = mesh.GetBlendShapeIndex(squares.Keys[k].Name);
            shaped = mesh;
            return true;
        }
    }

    /// <summary>
    /// Adds a <see cref="CrystalSpaceSquaresSO"/>'s shape keys to a mesh as Unity blend shapes. A body vertex at
    /// an anchor's position moves with that anchor; every other vertex holds still. Positions are exactly
    /// Blender's (a key is linear in its weight, so one frame would do); NORMALS are not linear - a square
    /// turning about a pentagon turns its normal with it - so each key carries four frames (weights 25, 50, 75,
    /// 100), each vertex's authored normal turned by the rotation its faces make at that weight, the same rule
    /// the lab's <c>squaresAttr()</c> uses. A key stacked on another is measured with that key at 1.
    /// </summary>
    internal static class CrystalSpaceSquaresBaker
    {
        static readonly float[] FrameWeights = { 0.25f, 0.5f, 0.75f, 1f };

        /// <summary>Anchors match body vertices to this fraction of the body's radius.</summary>
        const float MatchTolerance = 1e-4f;

        const string ShapedSuffix = "(Space Squares)";

        /// <summary>Shaped twins: (the mesh a body drew, the squares asset) -> that mesh with the keys added.</summary>
        static readonly Dictionary<(Mesh, CrystalSpaceSquaresSO), Mesh> s_shaped = new();

        // Enter Play Mode keeps statics (domain reload is off): a re-exported model is shaped afresh.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => s_shaped.Clear();

        /// <summary>
        /// The mesh a body drawing <paramref name="drawn"/> should wear to play <paramref name="squares"/>:
        /// <paramref name="drawn"/> itself when it already carries the keys (a cut twin of a shaped twin), else
        /// a shaped copy of it, made once and shared. Never edits <paramref name="drawn"/>, which is shared by
        /// every omni and may be the imported asset. The copy is registered with
        /// <see cref="RhombusSkinBaker.RegisterDerivedTwin"/> so <see cref="RhombusSkinBaker.SourceOf"/> still
        /// finds the FBX mesh behind it.
        /// </summary>
        public static bool TryGetShaped(Mesh drawn, CrystalSpaceSquaresSO squares, out Mesh shaped, out string problem)
        {
            shaped = null;
            if (!drawn)
            {
                problem = "the body draws no mesh";
                return false;
            }
            int carried = 0;
            foreach (var key in squares.Keys)
                if (drawn.GetBlendShapeIndex(key.Name) >= 0) carried++;
            if (carried == squares.Keys.Length)
            {
                shaped = drawn;
                problem = null;
                return true;
            }
            if (carried != 0)
            {
                problem = $"mesh '{drawn.name}' carries {carried} of the squares' {squares.Keys.Length} blend shapes";
                return false;
            }
            if (s_shaped.TryGetValue((drawn, squares), out shaped) && shaped)
            {
                problem = null;
                return true;
            }
            if (!drawn.isReadable)
            {
                problem = $"mesh '{drawn.name}' is not Read/Write enabled, so the squares cannot find their vertices";
                return false;
            }

            var twin = Object.Instantiate(drawn);
            string suffix = RhombusSkinBaker.BakedSuffix;
            // Keeps the skin twin's suffix last, so RhombusSkinBaker.TryDress still recognises a renderer wearing it.
            twin.name = drawn.name.EndsWith(suffix)
                ? $"{drawn.name[..^suffix.Length].TrimEnd()} {ShapedSuffix} {suffix}"
                : $"{drawn.name} {ShapedSuffix}";
            if (!TryAddShapes(twin, squares, out problem))
            {
                if (Application.isPlaying) Object.Destroy(twin);
                else Object.DestroyImmediate(twin);
                return false;
            }
            RhombusSkinBaker.RegisterDerivedTwin(twin, drawn);
            s_shaped[(drawn, squares)] = twin;
            shaped = twin;
            return true;
        }

        /// <summary>Adds the keys to <paramref name="mesh"/> in place (the edit-mode tests own what they shape).</summary>
        internal static bool TryAddShapes(Mesh mesh, CrystalSpaceSquaresSO squares, out string problem)
        {
            var keys = squares.Keys;
            var verts = mesh.vertices;
            var normals = mesh.normals;
            if (normals.Length != verts.Length)
            {
                problem = $"mesh '{mesh.name}' has no normals to turn";
                return false;
            }
            if (!TryMapAnchors(verts, squares.Anchors, squares.BodyRadius, out var anchorOf, out var axes, out problem))
            {
                problem = $"mesh '{mesh.name}': {problem}";
                return false;
            }

            int n = verts.Length;
            var full = new Vector3[keys.Length][];
            for (int k = 0; k < keys.Length; k++)
            {
                var deltas = keys[k].Deltas;
                if (deltas == null || deltas.Length != squares.Anchors.Length)
                {
                    problem = $"key '{keys[k].Name}' has {deltas?.Length ?? 0} offsets for {squares.Anchors.Length} anchors";
                    return false;
                }
                full[k] = new Vector3[n];
                for (int i = 0; i < n; i++)
                    if (anchorOf[i] >= 0) full[k][i] = Vector3.Scale(deltas[anchorOf[i]], axes);
            }

            var faces = new Faces(mesh, anchorOf);
            var restFaces = faces.Normals(verts);
            var position = new Vector3[n];
            var dv = new Vector3[n];
            var dn = new Vector3[n];
            for (int k = 0; k < keys.Length; k++)
            {
                // Where this key starts from: every key it is stacked on, at 1.
                var from = (Vector3[])verts.Clone();
                int guard = 0;
                for (int s = keys[k].StackedOn; s >= 0; s = keys[s].StackedOn)
                {
                    if (s >= keys.Length || ++guard > keys.Length)
                    {
                        problem = $"key '{keys[k].Name}' is stacked on a key that does not resolve";
                        return false;
                    }
                    for (int i = 0; i < n; i++) from[i] += full[s][i];
                }
                var fromNormals = faces.Turned(restFaces, from, normals);

                foreach (float w in FrameWeights)
                {
                    for (int i = 0; i < n; i++)
                    {
                        dv[i] = full[k][i] * w;
                        position[i] = from[i] + dv[i];
                    }
                    var turned = faces.Turned(restFaces, position, normals);
                    for (int i = 0; i < n; i++) dn[i] = turned[i] - fromNormals[i];
                    mesh.AddBlendShapeFrame(keys[k].Name, w * 100f, dv, dn, null);
                }
            }
            problem = null;
            return true;
        }

        /// <summary>
        /// Every vertex's anchor (-1 = it holds still), and the axes the anchors' offsets map onto the mesh with.
        /// The anchors are written in Unity's import space (x negated); a body imported at another scale is matched
        /// by the radius ratio, and one imported without the handedness flip by trying x unflipped.
        /// </summary>
        static bool TryMapAnchors(Vector3[] verts, Vector3[] anchors, float anchorRadius, out int[] anchorOf, out Vector3 axes, out string problem)
        {
            anchorOf = null;
            axes = Vector3.one;
            if (anchors is not { Length: > 0 })
            {
                problem = "the squares asset carries no anchors";
                return false;
            }
            float radius = 0f;
            foreach (var v in verts) radius = Mathf.Max(radius, v.magnitude);
            float scale = anchorRadius > 0f ? radius / anchorRadius : 1f;
            float tolerance = MatchTolerance * Mathf.Max(radius, 1e-6f);
            int bestHit = 0;
            foreach (float sx in new[] { 1f, -1f })
            {
                var candidate = new Vector3(sx * scale, scale, scale);
                var grid = new Dictionary<Vector3Int, int>(anchors.Length);
                for (int a = 0; a < anchors.Length; a++)
                    grid[Cell(Vector3.Scale(anchors[a], candidate), tolerance)] = a;
                var map = new int[verts.Length];
                var hit = new bool[anchors.Length];
                for (int i = 0; i < verts.Length; i++)
                {
                    map[i] = Nearest(grid, anchors, candidate, verts[i], tolerance);
                    if (map[i] >= 0) hit[map[i]] = true;
                }
                int hits = 0;
                foreach (bool h in hit) if (h) hits++;
                if (hits == anchors.Length)
                {
                    anchorOf = map;
                    axes = candidate;
                    problem = null;
                    return true;
                }
                bestHit = Mathf.Max(bestHit, hits);
            }
            problem = $"only {bestHit} of the squares' {anchors.Length} anchors sit on a body vertex - the body is not the mesh the squares were baked on";
            return false;
        }

        static Vector3Int Cell(Vector3 p, float tolerance)
        {
            float size = 2f * tolerance;
            return new Vector3Int(Mathf.RoundToInt(p.x / size), Mathf.RoundToInt(p.y / size), Mathf.RoundToInt(p.z / size));
        }

        static int Nearest(Dictionary<Vector3Int, int> grid, Vector3[] anchors, Vector3 axes, Vector3 p, float tolerance)
        {
            var c = Cell(p, tolerance);
            int best = -1;
            float bestSq = tolerance * tolerance;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!grid.TryGetValue(new Vector3Int(c.x + dx, c.y + dy, c.z + dz), out int a)) continue;
                float sq = (Vector3.Scale(anchors[a], axes) - p).sqrMagnitude;
                if (sq > bestSq) continue;
                bestSq = sq;
                best = a;
            }
            return best;
        }

        /// <summary>The triangles that touch a moving vertex, and how to turn its normal with them.</summary>
        sealed class Faces
        {
            readonly int[] triangles;          // every face touching a moving vertex, 3 indices each
            readonly int[][] facesOf;          // per moving vertex, its faces (into triangles / 3)

            public Faces(Mesh mesh, int[] anchorOf)
            {
                var tris = new List<int>();
                var touching = new Dictionary<int, List<int>>();
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var sub = mesh.GetTriangles(s);
                    for (int i = 0; i < sub.Length; i += 3)
                    {
                        if (anchorOf[sub[i]] < 0 && anchorOf[sub[i + 1]] < 0 && anchorOf[sub[i + 2]] < 0) continue;
                        int face = tris.Count / 3;
                        tris.Add(sub[i]);
                        tris.Add(sub[i + 1]);
                        tris.Add(sub[i + 2]);
                        for (int c = 0; c < 3; c++)
                        {
                            if (anchorOf[sub[i + c]] < 0) continue;
                            if (!touching.TryGetValue(sub[i + c], out var list)) touching[sub[i + c]] = list = new List<int>();
                            list.Add(face);
                        }
                    }
                }
                triangles = tris.ToArray();
                facesOf = new int[anchorOf.Length][];
                foreach (var pair in touching) facesOf[pair.Key] = pair.Value.ToArray();
            }

            public Vector3[] Normals(Vector3[] positions)
            {
                var result = new Vector3[triangles.Length / 3];
                for (int f = 0; f < result.Length; f++)
                {
                    var a = positions[triangles[3 * f]];
                    result[f] = Vector3.Cross(positions[triangles[3 * f + 1]] - a, positions[triangles[3 * f + 2]] - a);
                }
                return result;
            }

            /// <summary>Each moving vertex's normal, turned by the rotations its faces made from rest to
            /// <paramref name="positions"/> (averaged); a still vertex keeps its own.</summary>
            public Vector3[] Turned(Vector3[] restFaces, Vector3[] positions, Vector3[] normals)
            {
                var now = Normals(positions);
                var result = (Vector3[])normals.Clone();
                for (int i = 0; i < facesOf.Length; i++)
                {
                    var faces = facesOf[i];
                    if (faces == null) continue;
                    var sum = Vector3.zero;
                    foreach (int f in faces)
                    {
                        if (restFaces[f].sqrMagnitude < 1e-20f || now[f].sqrMagnitude < 1e-20f) continue;
                        sum += Quaternion.FromToRotation(restFaces[f], now[f]) * normals[i];
                    }
                    if (sum.sqrMagnitude > 1e-12f) result[i] = sum.normalized * normals[i].magnitude;
                }
                return result;
            }
        }
    }
}
