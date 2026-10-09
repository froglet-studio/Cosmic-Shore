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

        static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-12f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            return a + ab * t;
        }

        static bool RayCollider(Vector3 o, Vector3 d, Collider c, out float dist, out Vector3 normal)
        {
            dist = 0f; normal = -d;
            if (c is SphereCollider s)
            {
                var ctr = s.transform.TransformPoint(s.center);
                float r = WorldRadius(s);
                var oc = o - ctr;
                float b = Vector3.Dot(oc, d), cc = oc.sqrMagnitude - r * r;
                if (cc > 0f && b > 0f) return false;
                float disc = b * b - cc;
                if (disc < 0f) return false;
                dist = MathF.Max(0f, -b - MathF.Sqrt(disc));
                normal = (o + d * dist - ctr).normalized;
                return true;
            }
            var bounds = c.bounds;
            if (!bounds.IntersectRay(new Ray(o, d), out dist)) return false;
            var p = o + d * dist - bounds.center;
            var e = bounds.extents;
            float ax = MathF.Abs(p.x / MathF.Max(e.x, 1e-6f)), ay = MathF.Abs(p.y / MathF.Max(e.y, 1e-6f)), az = MathF.Abs(p.z / MathF.Max(e.z, 1e-6f));
            normal = ax >= ay && ax >= az ? new Vector3(MathF.Sign(p.x), 0, 0) : ay >= az ? new Vector3(0, MathF.Sign(p.y), 0) : new Vector3(0, 0, MathF.Sign(p.z));
            return true;
        }
    }

    public static partial class Physics
    {
        public const int DefaultRaycastLayers = ~(1 << 2);
        public const int IgnoreRaycastLayer = 1 << 2;
        public static bool queriesHitTriggers = true;
        public static Vector3 gravity = new(0f, -9.81f, 0f);
        public static bool autoSyncTransforms;

        /// <summary>The engine defaults, for a fresh world (the project's DynamicsManager is read after).</summary>
        internal static void ResetSettings()
        {
            queriesHitTriggers = true;
            gravity = new Vector3(0f, -9.81f, 0f);
            autoSyncTransforms = false;
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
            => Pass?.OverlapBox(center, halfExtents, layerMask, queryTriggerInteraction) ?? Array.Empty<Collider>();

        /// <summary>
        /// Original contract: fills <paramref name="results"/> with the colliders overlapping the box
        /// and returns how many, never more than the buffer holds. Same AABB convention as
        /// <see cref="OverlapBox"/> (orientation ignored, phase 2).
        /// </summary>
        public static int OverlapBoxNonAlloc(Vector3 center, Vector3 halfExtents, Collider[] results, Quaternion orientation = default,
            int layerMask = AllLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            var all = OverlapBox(center, halfExtents, orientation, layerMask, queryTriggerInteraction);
            int n = Math.Min(all.Length, results.Length);
            Array.Copy(all, results, n);
            return n;
        }

        public static bool CheckSphere(Vector3 position, float radius, int layerMask = AllLayers,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
            => OverlapSphereNonAlloc(position, radius, new Collider[1], layerMask, queryTriggerInteraction) > 0;

        public static void IgnoreCollision(Collider a, Collider b, bool ignore = true) { }
        public static void IgnoreLayerCollision(int layer1, int layer2, bool ignore = true) { }
        public static bool GetIgnoreLayerCollision(int layer1, int layer2) => false;
    }
}
