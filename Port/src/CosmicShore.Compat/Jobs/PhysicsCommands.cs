using System;
using CosmicShore.Engine.Collections;
using Unity.Jobs;

// UnityEngine's batched physics queries (original contract: QueryParameters, SpherecastCommand, OverlapCapsuleCommand). They are
// engine API, but ScheduleBatch returns Unity.Jobs.JobHandle, which lives here - so, like TransformJobs.cs,
// they sit beside the job types.
namespace CosmicShore.Engine
{
    /// <summary>What a batched query sees (original: UnityEngine.QueryParameters).</summary>
    public struct QueryParameters
    {
        public int layerMask;
        /// <summary>Unity: report every face of a mesh hit, not only the first. The port's shapes have one face per hit.</summary>
        public bool hitMultipleFaces;
        public QueryTriggerInteraction hitTriggers;
        /// <summary>Unity: hit triangles from behind. The port's shapes are closed volumes; ignored.</summary>
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

    /// <summary>
    /// One sphere cast of a batch (original: UnityEngine.SpherecastCommand). <see cref="ScheduleBatch(NativeArray{SpherecastCommand}, NativeArray{RaycastHit}, int, int, JobHandle)"/>
    /// runs every command at once, as the port runs every job (at Schedule, in index order).
    /// </summary>
    public struct SpherecastCommand
    {
        public Vector3 origin;
        public float radius;
        public Vector3 direction;
        public float distance;
        public QueryParameters queryParameters;

        public SpherecastCommand(Vector3 origin, float radius, Vector3 direction, QueryParameters queryParameters, float distance = float.MaxValue)
        {
            this.origin = origin;
            this.radius = radius;
            this.direction = direction;
            this.distance = distance;
            this.queryParameters = queryParameters;
        }

        /// <summary>Unity's single-hit overload: one result slot per command.</summary>
        public static JobHandle ScheduleBatch(NativeArray<SpherecastCommand> commands, NativeArray<RaycastHit> results, int minCommandsPerJob, JobHandle dependsOn = default)
            => ScheduleBatch(commands, results, minCommandsPerJob, 1, dependsOn);

        /// <summary>
        /// Unity's contract: command i writes its hits, nearest first, into results[i * maxHits ..]; a slot with no hit
        /// is left default (its collider null, Unity's end marker). The work is done here and the returned handle is
        /// already complete.
        ///
        /// APPROXIMATION: the port has no sphere sweep, so the sphere is the ray down its centre, lengthened by the
        /// radius as <see cref="Physics.SphereCast(Vector3, float, Vector3, out RaycastHit, float, int, QueryTriggerInteraction)"/>
        /// does, and each hit's distance is the ray's less the radius (clamped at 0, which Unity reports for a start
        /// overlap). A collider the sphere would graze off its centre line is missed.
        /// </summary>
        public static JobHandle ScheduleBatch(NativeArray<SpherecastCommand> commands, NativeArray<RaycastHit> results, int minCommandsPerJob, int maxHits, JobHandle dependsOn = default)
        {
            if (maxHits < 1) throw new ArgumentOutOfRangeException(nameof(maxHits), "maxHits must be at least 1");
            if (results.Length < commands.Length * maxHits)
                throw new ArgumentException($"SpherecastCommand.ScheduleBatch: {results.Length} result slots for {commands.Length} commands x {maxHits} hits");
            for (int c = 0; c < commands.Length; c++)
            {
                var cmd = commands[c];
                int start = c * maxHits, written = 0;
                float dirLen = cmd.direction.magnitude;
                if (cmd.distance > 0f && dirLen > 0f)
                {
                    var q = cmd.queryParameters;
                    var dir = cmd.direction / dirLen;
                    float r = Math.Max(0f, cmd.radius);
                    float reach = cmd.distance >= float.MaxValue ? float.PositiveInfinity : cmd.distance + r;
                    var hits = Physics.RaycastAll(cmd.origin, dir, reach, q.layerMask, q.hitTriggers);
                    for (int h = 0; h < hits.Length && written < maxHits; h++)
                    {
                        var hit = hits[h];
                        hit.distance = Math.Max(0f, hit.distance - r);
                        results[start + written++] = hit;
                    }
                }
                for (int k = written; k < maxHits; k++) results[start + k] = default;
            }
            return default;
        }
    }

    /// <summary>One collider an overlap query found (original: UnityEngine.ColliderHit).</summary>
    public readonly struct ColliderHit
    {
        readonly Collider _collider;
        public ColliderHit(Collider collider) => _collider = collider;
        public Collider collider => _collider;
        public int instanceID => _collider != null ? _collider.GetInstanceID() : 0;
    }

    /// <summary>
    /// One capsule overlap of a batch (original: UnityEngine.OverlapCapsuleCommand). ScheduleBatch runs every command at
    /// once through <see cref="Physics.OverlapCapsuleNonAlloc(Vector3, Vector3, float, Collider[], int, QueryTriggerInteraction)"/>.
    /// </summary>
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

        /// <summary>
        /// Unity's contract: command i writes up to <paramref name="maxHits"/> colliders into results[i * maxHits ..]; an
        /// unused slot is left default (a null collider). The work is done here and the returned handle is complete.
        /// </summary>
        public static JobHandle ScheduleBatch(NativeArray<OverlapCapsuleCommand> commands, NativeArray<ColliderHit> results, int minCommandsPerJob, int maxHits, JobHandle dependsOn = default)
        {
            if (maxHits < 1) throw new ArgumentOutOfRangeException(nameof(maxHits), "maxHits must be at least 1");
            if (results.Length < commands.Length * maxHits)
                throw new ArgumentException($"OverlapCapsuleCommand.ScheduleBatch: {results.Length} result slots for {commands.Length} commands x {maxHits} hits");
            var buffer = new Collider[maxHits];
            for (int c = 0; c < commands.Length; c++)
            {
                var cmd = commands[c];
                var q = cmd.queryParameters;
                int n = Physics.OverlapCapsuleNonAlloc(cmd.point0, cmd.point1, cmd.radius, buffer, q.layerMask, q.hitTriggers);
                for (int k = 0; k < maxHits; k++)
                {
                    results[c * maxHits + k] = k < n ? new ColliderHit(buffer[k]) : default;
                    buffer[k] = null;
                }
            }
            return default;
        }
    }
}
