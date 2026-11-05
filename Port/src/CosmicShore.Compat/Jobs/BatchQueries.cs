using System;
using CosmicShore.Engine.Collections;
using Unity.Jobs;

// UnityEngine's batched physics queries (original contract: SpherecastCommand, OverlapCapsuleCommand, QueryParameters,
// ColliderHit). They are engine API, but their schedules take NativeArrays and return Unity.Jobs.JobHandle, which live
// here - so they sit beside the job types, as TransformJobs does. The port runs jobs synchronously at Schedule
// (deterministic, command order), so each batch is answered in full before ScheduleBatch returns, through the loop's
// own queries (Physics.SphereCastNonAlloc, Physics.OverlapCapsuleNonAlloc).
namespace CosmicShore.Engine
{
    /// <summary>What a batched query sees (original contract: UnityEngine.QueryParameters).</summary>
    public struct QueryParameters
    {
        public int layerMask;
        public bool hitMultipleFaces;
        public QueryTriggerInteraction hitTriggers;
        public bool hitBackfaces;

        public QueryParameters(int layerMask = Physics.DefaultRaycastLayers, bool hitMultipleFaces = false,
            QueryTriggerInteraction hitTriggers = QueryTriggerInteraction.UseGlobal, bool hitBackfaces = false)
        {
            this.layerMask = layerMask;
            this.hitMultipleFaces = hitMultipleFaces;
            this.hitTriggers = hitTriggers;
            this.hitBackfaces = hitBackfaces;
        }

        public static QueryParameters Default => new(Physics.DefaultRaycastLayers, false, QueryTriggerInteraction.UseGlobal, false);
    }

    /// <summary>One collider an overlap command found (original contract: UnityEngine.ColliderHit). instanceID 0 = none.</summary>
    public readonly struct ColliderHit
    {
        readonly Collider _collider;

        public ColliderHit(Collider collider) => _collider = collider;

        public int instanceID => _collider is null ? 0 : _collider.GetInstanceID();
        public Collider collider => _collider;
    }

    /// <summary>A sphere cast to run in a batch (original contract: UnityEngine.SpherecastCommand).</summary>
    public struct SpherecastCommand
    {
        public Vector3 origin;
        public float radius;
        public Vector3 direction;
        public float distance;
        public QueryParameters queryParameters;

        public SpherecastCommand(Vector3 origin, float radius, Vector3 direction, QueryParameters queryParameters,
            float distance = float.MaxValue)
        {
            this.origin = origin;
            this.radius = radius;
            this.direction = direction;
            this.queryParameters = queryParameters;
            this.distance = distance;
        }

        /// <summary>
        /// Runs every command now. Command i writes its hits to results[i * maxHits ...], nearest first, and an unused slot
        /// is left default (collider null, distance 0), as Unity leaves it.
        /// </summary>
        public static JobHandle ScheduleBatch(NativeArray<SpherecastCommand> commands, NativeArray<RaycastHit> results,
            int minCommandsPerJob, int maxHits, JobHandle dependsOn = default)
        {
            if (maxHits < 1) throw new ArgumentException("maxHits must be at least 1", nameof(maxHits));
            var buffer = new RaycastHit[maxHits];
            for (int i = 0; i < commands.Length; i++)
            {
                var c = commands[i];
                int n = Physics.SphereCastNonAlloc(c.origin, c.radius, c.direction, buffer, c.distance,
                    c.queryParameters.layerMask, c.queryParameters.hitTriggers);
                int b = i * maxHits;
                for (int h = 0; h < maxHits && b + h < results.Length; h++)
                    results[b + h] = h < n ? buffer[h] : default;
            }
            return dependsOn;
        }

        public static JobHandle ScheduleBatch(NativeArray<SpherecastCommand> commands, NativeArray<RaycastHit> results,
            int minCommandsPerJob, JobHandle dependsOn = default)
            => ScheduleBatch(commands, results, minCommandsPerJob, 1, dependsOn);
    }

    /// <summary>A capsule overlap to run in a batch (original contract: UnityEngine.OverlapCapsuleCommand).</summary>
    public struct OverlapCapsuleCommand
    {
        public Vector3 point0;
        public Vector3 point1;
        public float radius;
        public QueryParameters queryParameters;

        public OverlapCapsuleCommand(Vector3 point0, Vector3 point1, float radius, QueryParameters queryParameters)
        {
            this.point0 = point0;
            this.point1 = point1;
            this.radius = radius;
            this.queryParameters = queryParameters;
        }

        /// <summary>Runs every command now; command i writes its colliders to results[i * maxHits ...], unused slots default.</summary>
        public static JobHandle ScheduleBatch(NativeArray<OverlapCapsuleCommand> commands, NativeArray<ColliderHit> results,
            int minCommandsPerJob, int maxHits, JobHandle dependsOn = default)
        {
            if (maxHits < 1) throw new ArgumentException("maxHits must be at least 1", nameof(maxHits));
            var buffer = new Collider[maxHits];
            for (int i = 0; i < commands.Length; i++)
            {
                var c = commands[i];
                int n = Physics.OverlapCapsuleNonAlloc(c.point0, c.point1, c.radius, buffer, c.queryParameters.layerMask,
                    c.queryParameters.hitTriggers);
                int b = i * maxHits;
                for (int h = 0; h < maxHits && b + h < results.Length; h++)
                    results[b + h] = h < n ? new ColliderHit(buffer[h]) : default;
            }
            return dependsOn;
        }
    }
}
