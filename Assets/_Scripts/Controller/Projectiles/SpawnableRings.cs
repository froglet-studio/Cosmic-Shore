using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lays ring(s) of skimmable boost prisms around the caster's flight path, via the shared
    /// <see cref="BoostRingBuilder"/>. Driven by <see cref="AOEBlockSpawner"/> on the two AOE ring
    /// prefabs: <c>AOEShieldedRingSpawner</c> (Squirrel omnicrystal hit, shielded) and
    /// <c>AOEDangerRingSpawner</c> (joust, danger).
    ///
    /// The geometry is the wide-open "fast" arrangement at ALL speeds — prisms long-side along the
    /// flight axis, hollow centre always flyable. The old version tilted each prism toward the ring
    /// centre by <c>speed * 0.3°</c>, so a slow vessel got a closed spoke-wheel it couldn't fly
    /// through; that tilt is gone. Prisms come from the Boost pool with full-size colliders from
    /// frame 0 (see <see cref="BoostRingBuilder"/>), so the skim is deterministic at any speed.
    /// </summary>
    public class SpawnableRings : SpawnableBase
    {
        [Header("Ring Configuration")]
        [SerializeField] int ringCount = 3;
        [SerializeField] int prismsPerRing = 8;
        [SerializeField] float ringRadius = 20f;
        [SerializeField] float ringSpacing = 15f;
        float initialOffset = 8;

        [Header("Prism Configuration")]
        [SerializeField] Vector3 prismScale = new Vector3(4, 4, 9);

        [Header("Prism Properties")]
        [SerializeField] bool isDangerous = false;
        [SerializeField] bool isShielded = false;

        [Header("Events")]
        [Tooltip("Pooled-prism spawn channel (EventOnSpawnPrismAndReturn). Rings are laid from the " +
                 "dedicated Boost pool — fast bloom, full-size collider from frame 0 — never " +
                 "Instantiated.")]
        [SerializeField] PrismEventChannelWithReturnSO prismSpawnEvent;

        PrismKind Kind =>
            isDangerous ? PrismKind.Danger :
            isShielded ? PrismKind.Shielded :
            PrismKind.Plain;

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(seed, ringCount, prismsPerRing, ringRadius,
                System.HashCode.Combine(ringSpacing, prismScale, isDangerous, isShielded));
        }

        public override GameObject Spawn(int intensity = 1)
        {
            intensityLevel = intensity;
            trails.Clear();

            // Pose anchor + owner prefix only — the pooled prisms live under the Boost pool, not
            // here, so destroying the spawner never destroys the laid mass.
            GameObject container = new GameObject($"Rings_{name}");
            container.transform.SetParent(transform, false);

            var spec = new BoostRingSpec(prismsPerRing, ringRadius, prismScale, Kind);

            for (int ringIndex = 0; ringIndex < ringCount; ringIndex++)
            {
                Trail trail = new Trail();
                trails.Add(trail);

                Vector3 ringCenter = transform.position + transform.forward * (ringIndex * ringSpacing + initialOffset);
                BoostRingBuilder.LayRing(prismSpawnEvent, new Pose(ringCenter, transform.rotation), spec,
                    domain, playerName: null, $"{container.name}::R{ringIndex}", trail);
            }

            return container;
        }
    }
}
