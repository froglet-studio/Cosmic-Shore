using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Geometry + kind for one boost ring: how many prisms, how wide, how thick, what state.</summary>
    public readonly struct BoostRingSpec
    {
        public readonly int Segments;
        public readonly float Radius;
        public readonly Vector3 PrismScale;
        public readonly PrismKind Kind;

        public BoostRingSpec(int segments, float radius, Vector3 prismScale, PrismKind kind)
        {
            Segments = Mathf.Max(3, segments);
            Radius = radius;
            PrismScale = prismScale;
            Kind = kind;
        }
    }

    /// <summary>
    /// THE canonical "ring of prisms the skimmer WILL collide with" builder - shared by every
    /// feature that throws a fly-through boost ring around the flight path: the Squirrel
    /// omnicrystal ring (<c>SpawnableRings</c> via <c>AOEShieldedRingSpawner</c>), the joust ring
    /// (<c>SpawnableRings</c> via <c>AOEDangerRingSpawner</c>), and the Squirrel tube ability
    /// (<c>SquirrelTubeActionExecutor</c>). Fix ring behaviour here once; all three follow.
    ///
    /// The two guarantees that make the skim deterministic:
    ///   1. <b>Full-size collider from frame 0.</b> Every prism comes from the dedicated Boost pool
    ///      (<see cref="PrismType.Boost"/>: waitTime 0, fast bloom) and is put under
    ///      <see cref="Prism.HoldColliderAtFullSize"/>, so its collider covers the full target
    ///      world size the frame it spawns while the visual blooms in - no speed outruns it.
    ///   2. <b>Speed-independent open geometry.</b> Prisms lie with their long side ALONG the ring
    ///      axis ("up" pointing outward radially) - the wide-open arrangement the old speed-tilted
    ///      ring only reached when fast - so the centre is always flyable and the wall is always
    ///      presented broadside to a skimmer coming down the axis.
    ///
    /// Shielded/SuperShielded kinds are applied only AFTER the bloom completes (via the
    /// <c>onGrown</c> callback): shield engage swaps to the octahedron MeshCollider, which scales
    /// with the transform and would defeat the full-size hold during the bloom. Danger repaints on
    /// the BoxCollider, so it applies immediately.
    /// </summary>
    public static class BoostRingBuilder
    {
        /// <summary>
        /// Lays one ring of <see cref="BoostRingSpec.Segments"/> boost prisms around
        /// <paramref name="pose"/>'s forward axis. Pass a <paramref name="trail"/> to group them,
        /// and/or <paramref name="collected"/> to track them for later teardown.
        /// </summary>
        public static void LayRing(PrismEventChannelWithReturnSO channel, Pose pose, in BoostRingSpec spec,
            Domains domain, string playerName, string ownerPrefix,
            Trail trail = null, List<Prism> collected = null)
        {
            if (!channel)
            {
                CSDebug.LogWarning("[BoostRingBuilder] Prism spawn channel not wired - cannot lay ring.");
                return;
            }

            for (int i = 0; i < spec.Segments; i++)
            {
                float angle = i * (2f * Mathf.PI / spec.Segments);
                Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector3 position = pose.position + pose.rotation * (radial * spec.Radius);
                // Long side runs along the ring axis; block "up" points outward radially.
                Quaternion rotation = pose.rotation * Quaternion.LookRotation(Vector3.forward, radial);

                LayOne(channel, position, rotation, spec.PrismScale, spec.Kind,
                    domain, playerName, $"{ownerPrefix}::{i}", trail, collected);
            }
        }

        /// <summary>
        /// The one place a boost prism is born: Boost pool spawn → team → owner → target scale →
        /// trail → Initialize → kind → full-size collider hold. Mirrors
        /// <see cref="PrismTrailBuilder.LayOne"/> for the pooled path.
        /// </summary>
        public static Prism LayOne(PrismEventChannelWithReturnSO channel, Vector3 position, Quaternion rotation,
            Vector3 scale, PrismKind kind, Domains domain, string playerName, string ownerId,
            Trail trail = null, List<Prism> collected = null)
        {
            var ret = channel.RaiseEvent(new PrismEventData
            {
                ownDomain = domain,
                Rotation = rotation,
                SpawnPosition = position,
                Scale = scale,
                PrismType = PrismType.Boost
            });

            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism))
                return null;

            prism.ChangeTeam(domain);
            prism.ownerID = ownerId;
            prism.TargetScale = scale;

            if (string.IsNullOrEmpty(playerName)) prism.Initialize(); // environment-owned
            else prism.Initialize(playerName);

            // AFTER Initialize - pool-reuse reset clears trail membership, so a stamp made
            // before it is silently wiped and the ring's prisms become container-less. That
            // reads as a 0D Singleton to the prismscape topology, which is how a boost ring
            // stopped being rideable as the 1D LOOP it is.
            if (trail != null) prism.AssignTrail(trail);

            // Danger repaints on the BoxCollider - apply now so the ring reads as a hazard from
            // frame 0. Shield kinds swap to the transform-scaled octahedron MeshCollider, so they
            // wait for the bloom to finish (the hold guarantees the box footprint until then).
            bool deferredKind = kind is PrismKind.Shielded or PrismKind.SuperShielded;
            if (!deferredKind)
                PrismKinds.Apply(prism, kind);

            prism.HoldColliderAtFullSize(deferredKind ? () => PrismKinds.Apply(prism, kind) : null);

            trail?.Add(prism);
            collected?.Add(prism);
            return prism;
        }
    }
}
