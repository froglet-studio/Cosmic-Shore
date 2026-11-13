using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure half of the petal theft cue (<see cref="PetalTheftFeedback"/>,
    /// Docs/ElementalAbilitySystem/PETAL_THEFT_CUE.md): how many shards a hit draws, how big, how long
    /// the stream takes, how a shard steers onto a moving attacker, and the shard's faceted mesh. No
    /// scene, no state, so the editor tests drive it directly.
    /// </summary>
    public static class PetalTheftShapes
    {
        /// <summary>Shards a hit worth <paramref name="petals"/> draws: <paramref name="perPetal"/> each,
        /// rounded, clamped to [<paramref name="min"/>, <paramref name="max"/>]. Nothing for a worthless hit.</summary>
        public static int ShardCount(float petals, float perPetal, int min, int max)
        {
            if (petals <= 0f || max <= 0) return 0;
            int n = Mathf.RoundToInt(petals * Mathf.Max(0f, perPetal));
            return Mathf.Clamp(n, Mathf.Max(0, min), Mathf.Max(min, max));
        }

        /// <summary>A shard's length for a hull of <paramref name="hullRadius"/>: a share of the hull,
        /// clamped to world sizes that read from a wide camera.</summary>
        public static float ShardSize(float hullRadius, float shareOfHull, float min, float max) =>
            Mathf.Clamp(Mathf.Max(0f, hullRadius) * shareOfHull, min, Mathf.Max(min, max));

        /// <summary>Seconds the stream takes to cover <paramref name="distance"/> at
        /// <paramref name="cruiseSpeed"/>, clamped so a point-blank hit still shows the flow and a
        /// sniper's theft lands while it still matters.</summary>
        public static float TravelSeconds(float distance, float cruiseSpeed, float min, float max) =>
            Mathf.Clamp(Mathf.Max(0f, distance) / Mathf.Max(1f, cruiseSpeed), min, Mathf.Max(min, max));

        /// <summary>
        /// The velocity a shard wants this frame to reach a target at <paramref name="target"/>, moving at
        /// <paramref name="targetVelocity"/>, in <paramref name="secondsLeft"/>: close the gap in the time
        /// left, plus the target's own motion, so a shard catches an attacker flying at full boost.
        /// </summary>
        public static Vector3 DesiredVelocity(Vector3 position, Vector3 target, Vector3 targetVelocity, float secondsLeft) =>
            (target - position) / Mathf.Max(0.08f, secondsLeft) + targetVelocity;

        /// <summary>One steering step: <paramref name="velocity"/> eased toward <paramref name="desired"/>
        /// at <paramref name="steer"/> per second, frame-rate independent.</summary>
        public static Vector3 Steer(Vector3 velocity, Vector3 desired, float steer, float dt) =>
            Vector3.Lerp(velocity, desired, 1f - Mathf.Exp(-Mathf.Max(0f, steer) * Mathf.Max(0f, dt)));

        /// <summary>How many of <paramref name="count"/> dark shards a silenced hull shows with
        /// <paramref name="remainingSeconds"/> of <paramref name="maxSeconds"/> left: at least one while
        /// silenced, all of them at the ceiling, none once it has run out.</summary>
        public static int SilenceShardsShown(float remainingSeconds, float maxSeconds, int count)
        {
            if (remainingSeconds <= 0f || count <= 0) return 0;
            if (maxSeconds <= 0f) return count;
            return Mathf.Clamp(Mathf.CeilToInt(count * remainingSeconds / maxSeconds), 1, count);
        }

        /// <summary>A flash's strength for a hit worth <paramref name="petals"/>: full at four petals or
        /// more (a missile shockwave), never below <paramref name="floor"/>.</summary>
        public static float FlashStrength(float petals, float floor) =>
            petals <= 0f ? 0f : Mathf.Clamp(petals / 4f, Mathf.Clamp01(floor), 1f);

        /// <summary>
        /// The shard: an elongated, slightly uneven hexagonal bipyramid along +Z, one unit long, with a
        /// sharp front point and a blunter tail, FLAT-SHADED (every facet its own vertices and normal) so
        /// the scene's light picks out its cuts. A faceted volume, the prism-cut read of the crystals and
        /// hulls, never a sprite (Docs/claude/SHADERS_AND_PERFORMANCE.md, "Nothing in the world looks flat").
        /// </summary>
        public static Mesh BuildShardMesh()
        {
            const int sides = 6;
            const float front = 0.62f, back = -0.38f, girth = 0.2f;
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                // Uneven on purpose: alternate facets wider, so the cuts catch light at different angles.
                float r = girth * (i % 2 == 0 ? 1f : 0.72f);
                ring[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0.04f * (i % 3 - 1));
            }

            var tip = new Vector3(0f, 0f, front);
            var tail = new Vector3(0f, 0f, back);
            var vertices = new Vector3[sides * 2 * 3];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[vertices.Length];
            int v = 0;
            for (int i = 0; i < sides; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % sides];
                // Wound so Cross(b - a, c - a) points OUT of the solid: Unity's front face (clockwise
                // seen from outside), and the normal RecalculateNormals would give it.
                v = Facet(vertices, normals, triangles, v, tip, a, b);
                v = Facet(vertices, normals, triangles, v, tail, b, a);
            }

            var mesh = new Mesh { name = "PetalTheftShard" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        static int Facet(Vector3[] vertices, Vector3[] normals, int[] triangles, int v, Vector3 a, Vector3 b, Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            vertices[v] = a; vertices[v + 1] = b; vertices[v + 2] = c;
            normals[v] = normals[v + 1] = normals[v + 2] = n;
            triangles[v] = v; triangles[v + 1] = v + 1; triangles[v + 2] = v + 2;
            return v + 3;
        }

        /// <summary>
        /// True when a mesh is a VOLUME and not a flat sheet: its facet normals point in many directions
        /// (no single axis they all share), and it stands well out of every plane through its centre.
        /// A billboard or a flat star fails both.
        /// </summary>
        public static bool IsVolume(Mesh mesh)
        {
            if (!mesh) return false;
            var normals = mesh.normals;
            var verts = mesh.vertices;
            if (normals.Length < 9) return false;

            // Facet normals must span 3D: the smallest extent of their spread is not tiny.
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int i = 0; i < normals.Length; i++)
            {
                min = Vector3.Min(min, normals[i]);
                max = Vector3.Max(max, normals[i]);
            }
            var spread = max - min;
            if (Mathf.Min(spread.x, Mathf.Min(spread.y, spread.z)) < 0.5f) return false;

            // And the shape itself has thickness on every axis.
            var size = mesh.bounds.size;
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float thinnest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            return verts.Length > 0 && longest > 0f && thinnest / longest > 0.15f;
        }
    }
}
