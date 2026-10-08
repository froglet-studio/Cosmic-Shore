using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>Which trigger colliders a query sees (original: UnityEngine.QueryTriggerInteraction).</summary>
    public enum QueryTriggerInteraction { UseGlobal = 0, Ignore = 1, Collide = 2 }

    public sealed partial class TriggerPass
    {
        static bool Accepts(Collider c, int layerMask, QueryTriggerInteraction qti)
        {
            if (!c.isActiveAndEnabled) return false;
            if ((layerMask & (1 << c.gameObject.layer)) == 0) return false;
            if (c.isTrigger && qti == QueryTriggerInteraction.Ignore) return false;
            if (c.isTrigger && qti == QueryTriggerInteraction.UseGlobal && !Physics.queriesHitTriggers) return false;
            return true;
        }

        static bool RayCollider(Vector3 o, Vector3 d, Collider c, out float dist, out Vector3 normal)
        {
            dist = 0f; normal = -d;
            return ShapeMath.TryBuild(c, out var shape) && ShapeMath.Raycast(in shape, o, d, out dist, out normal);
        }
    }

    public static partial class Physics
    {
        public const int DefaultRaycastLayers = ~(1 << 2);
        public const int IgnoreRaycastLayer = 1 << 2;
        public static bool queriesHitTriggers = true;
        public static Vector3 gravity = new(0f, -9.81f, 0f);
        public static bool autoSyncTransforms;

        /// <summary>Approach speed below which a contact does not bounce (DynamicsManager m_BounceThreshold).</summary>
        public static float bounceThreshold = 2f;

        // The layer collision matrix: bit j of row i set = layers i and j make contact. Read by the
        // contact pass only (the trigger pass does not filter by layer).
        static readonly uint[] s_layerMatrix = NewMatrix();
        static readonly HashSet<(Collider, Collider)> s_ignoredPairs = new();

        static uint[] NewMatrix() { var m = new uint[32]; Array.Fill(m, uint.MaxValue); return m; }

        /// <summary>The engine defaults, for a fresh world (the project's DynamicsManager is read after).</summary>
        internal static void ResetSettings()
        {
            queriesHitTriggers = true;
            gravity = new Vector3(0f, -9.81f, 0f);
            autoSyncTransforms = false;
            bounceThreshold = 2f;
            Array.Fill(s_layerMatrix, uint.MaxValue);
            s_ignoredPairs.Clear();
        }

        /// <summary>Loads DynamicsManager's m_LayerCollisionMatrix: 32 rows, each a little-endian uint in 8 hex digits.</summary>
        public static void SetLayerCollisionMatrix(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return;
            for (int row = 0; row < 32 && (row + 1) * 8 <= hex.Length; row++)
            {
                uint v = 0;
                for (int b = 0; b < 4; b++)
                    v |= (uint)Convert.ToByte(hex.Substring(row * 8 + b * 2, 2), 16) << (8 * b);
                s_layerMatrix[row] = v;
            }
        }
        /// <summary>Moves every collider in the query scene to its transform's current pose (see TriggerPass snapshot).</summary>
        public static void SyncTransforms() => Pass?.SyncQuerySnapshot();

        static TriggerPass Pass => GameLoop.Current?.Triggers;

        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance, int layerMask,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            hitInfo = default;
            return Pass?.Raycast(origin, direction.normalized, out hitInfo, maxDistance, layerMask, queryTriggerInteraction) ?? false;
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
            => Raycast(origin, direction, out _, maxDistance, layerMask, queryTriggerInteraction);

        public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity, int layerMask = DefaultRaycastLayers,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
            => Raycast(ray.origin, ray.direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);

        public static RaycastHit[] RaycastAll(Vector3 origin, Vector3 direction, float maxDistance = float.PositiveInfinity, int layerMask = DefaultRaycastLayers,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
            => Pass?.RaycastAll(origin, direction.normalized, maxDistance, layerMask, queryTriggerInteraction) ?? Array.Empty<RaycastHit>();

        public static int RaycastNonAlloc(Vector3 origin, Vector3 direction, RaycastHit[] results, float maxDistance = float.PositiveInfinity,
            int layerMask = DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            var all = RaycastAll(origin, direction, maxDistance, layerMask, queryTriggerInteraction);
            int n = Math.Min(all.Length, results.Length);
            Array.Copy(all, results, n);
            return n;
        }

        public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance = float.PositiveInfinity,
            int layerMask = DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
            => Raycast(origin, direction, out hitInfo, maxDistance + radius, layerMask, queryTriggerInteraction);

        public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
            => Pass?.OverlapSphereNonAlloc(position, radius, results, layerMask, queryTriggerInteraction) ?? 0;

        public static Collider[] OverlapSphere(Vector3 position, float radius, int layerMask, QueryTriggerInteraction queryTriggerInteraction)
        {
            var buffer = new Collider[1024];
            int n = OverlapSphereNonAlloc(position, radius, buffer, layerMask, queryTriggerInteraction);
            Array.Resize(ref buffer, n);
            return buffer;
        }

        public static int OverlapCapsuleNonAlloc(Vector3 point0, Vector3 point1, float radius, Collider[] results, int layerMask = AllLayers,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
            => Pass?.OverlapCapsuleNonAlloc(point0, point1, radius, results, layerMask, queryTriggerInteraction) ?? 0;

        public static Collider[] OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation = default, int layerMask = AllLayers,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
            => Pass?.OverlapBox(center, halfExtents, orientation, layerMask, queryTriggerInteraction) ?? Array.Empty<Collider>();

        public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation = default, int layerMask = AllLayers,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
            => Pass?.OverlapBoxNonAlloc(center, halfExtents, results, orientation, layerMask, queryTriggerInteraction) ?? 0;

        public static bool CheckSphere(Vector3 position, float radius, int layerMask = AllLayers,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
            => OverlapSphereNonAlloc(position, radius, new Collider[1], layerMask, queryTriggerInteraction) > 0;

        /// <summary>Contacts between these two colliders are skipped (contact pass; triggers are unaffected).</summary>
        public static void IgnoreCollision(Collider a, Collider b, bool ignore = true)
        {
            if (a is null || b is null) return;
            var key = PairKey(a, b);
            if (ignore) s_ignoredPairs.Add(key); else s_ignoredPairs.Remove(key);
        }

        public static bool GetIgnoreCollision(Collider a, Collider b) => IsCollisionIgnored(a, b);

        internal static bool IsCollisionIgnored(Collider a, Collider b)
            => s_ignoredPairs.Count > 0 && s_ignoredPairs.Contains(PairKey(a, b));

        static (Collider, Collider) PairKey(Collider a, Collider b)
            => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(a) <= System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(b) ? (a, b) : (b, a);

        public static void IgnoreLayerCollision(int layer1, int layer2, bool ignore = true)
        {
            if ((uint)layer1 > 31 || (uint)layer2 > 31) return;
            if (ignore) { s_layerMatrix[layer1] &= ~(1u << layer2); s_layerMatrix[layer2] &= ~(1u << layer1); }
            else { s_layerMatrix[layer1] |= 1u << layer2; s_layerMatrix[layer2] |= 1u << layer1; }
        }

        public static bool GetIgnoreLayerCollision(int layer1, int layer2)
            => (uint)layer1 <= 31 && (uint)layer2 <= 31 && (s_layerMatrix[layer1] & (1u << layer2)) == 0;
    }
}
