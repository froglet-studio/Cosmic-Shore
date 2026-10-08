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

    /// <summary>One ring that has just been laid: where it went, what it is, and its prisms.</summary>
    public readonly struct BoostRingLay
    {
        public readonly Pose Pose;
        public readonly BoostRingSpec Spec;
        public readonly Domains Domain;
        /// <summary>The prisms, in ring order. Live, conserved mass — read it, never retire it.</summary>
        public readonly IReadOnlyList<Prism> Prisms;

        public BoostRingLay(Pose pose, in BoostRingSpec spec, Domains domain, IReadOnlyList<Prism> prisms)
        {
            Pose = pose;
            Spec = spec;
            Domain = domain;
            Prisms = prisms;
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
    ///      (<see cref="PrismType.Boost"/>: waitTime 0, fast bloom). Under the clock-material law
    ///      the transform is FINAL at stamp, so the authored BoxCollider is already a full-size
    ///      world footprint from frame 0 while the GPU blooms the visual — no per-frame collider
    ///      compensation (the retired <c>HoldColliderAtFullSize</c>).
    ///   2. <b>Speed-independent open geometry.</b> Prisms lie with their long side ALONG the ring
    ///      axis ("up" pointing outward radially) - the wide-open arrangement the old speed-tilted
    ///      ring only reached when fast - so the centre is always flyable and the wall is always
    ///      presented broadside to a skimmer coming down the axis.
    ///
    /// All kinds (including Shielded/SuperShielded) apply immediately: with transform-at-final
    /// from stamp, shield shells are full-size at frame 0 and do not need a deferred onGrown
    /// callback. Danger still repaints on the BoxCollider.
    /// </summary>
    public static class BoostRingBuilder
    {
        /// <summary>
        /// Raised the instant a ring finishes laying, on the machine that laid it.
        ///
        /// It exists so a visual that has to LAND on a ring reads the ring the builder actually
        /// made instead of re-deriving it from the same authored numbers — those two can drift,
        /// the ring cannot drift from itself. The Squirrel's omni-crystal morph
        /// (<see cref="SquirrelCrystalMorph"/>) is the listener: it ends on the real octahedra of
        /// the real prisms, so retuning <c>SpawnableRings</c> moves the animation with it.
        ///
        /// Listeners are visuals: they may hold a prism's PHOTONS, never its mass.
        /// </summary>
        public static event System.Action<BoostRingLay> RingLaid;

        // Fast enter-play-mode keeps statics: a listener stranded by a stopped session must not be
        // called into the next one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => RingLaid = null;

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

            // Gathered only when somebody is listening, so a ring nobody watches allocates nothing.
            var laid = RingLaid != null ? new List<Prism>(spec.Segments) : null;

            for (int i = 0; i < spec.Segments; i++)
            {
                float angle = i * (2f * Mathf.PI / spec.Segments);
                Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector3 position = pose.position + pose.rotation * (radial * spec.Radius);
                // Long side runs along the ring axis; block "up" points outward radially.
                Quaternion rotation = pose.rotation * Quaternion.LookRotation(Vector3.forward, radial);

                var prism = LayOne(channel, position, rotation, spec.PrismScale, spec.Kind,
                    domain, playerName, $"{ownerPrefix}::{i}", trail, collected);
                if (prism) laid?.Add(prism);
            }

            // ISOLATED: a listener is a VISUAL, and a visual must never be able to damage conserved
            // mass. A throwing listener would otherwise unwind out of the lay into whatever spawner
            // called it, and be reported frames from its cause — which is how the Squirrel's morph
            // first failed (an unreadable source mesh threw in here after the prisms were laid, and
            // the only symptom was the crystal fading out).
            if (laid is { Count: > 0 })
            {
                try
                {
                    RingLaid?.Invoke(new BoostRingLay(pose, spec, domain, laid));
                }
                catch (System.Exception e)
                {
                    CSDebug.LogError($"[BoostRingBuilder] a RingLaid listener threw; the ring is laid " +
                                     $"and unaffected. {e}");
                }
            }
        }

        /// <summary>
        /// The one place a boost prism is born: Boost pool spawn → team → owner → target scale →
        /// trail → Initialize → kind (full-size collider from stamp). Mirrors
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

            // Transform is final at stamp under the clock law — apply every kind immediately
            // (including shield shells, which are full-size from frame 0).
            PrismKinds.Apply(prism, kind);

            trail?.Add(prism);
            collected?.Add(prism);
            return prism;
        }
    }
}
