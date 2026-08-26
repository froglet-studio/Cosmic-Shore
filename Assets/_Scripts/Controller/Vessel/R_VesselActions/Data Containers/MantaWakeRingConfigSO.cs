using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tuning for the Manta's SOAR wake rings — the Time element's qualitative payoff. While
    /// the Manta soars (both triggers), it periodically lays a boost ring behind itself:
    /// ordinary conserved prism mass in its own domain, with a SWITCH in the hole (the
    /// platform's ring-you-thread fundamental) that pays a velocity surge to whoever threads
    /// it. Below Time 5 the rings are sparse and pay only the Manta that laid them; at Time 5
    /// ("Wake Highway") they come twice as often, pay harder, and any own-domain vessel can
    /// ride them — a highway the team can follow. Snapshotted per ring at LAY time.
    /// </summary>
    [CreateAssetMenu(fileName = "MantaWakeRingConfig",
        menuName = "ScriptableObjects/Vessel Actions/Manta Wake Ring Config")]
    public class MantaWakeRingConfigSO : ScriptableObject
    {
        [Header("Ring geometry")]
        [SerializeField, Min(3)] int segments = 8;
        [SerializeField, Min(5f)] float ringRadius = 18f;
        [SerializeField] Vector3 prismScale = new Vector3(10f, 1.5f, 4f);
        [Tooltip("How far BEHIND the vessel (along its course) each ring is laid.")]
        [SerializeField, Min(0f)] float behindOffset = 30f;

        [Header("Cadence (while soaring)")]
        [Tooltip("Seconds between rings at base Time.")]
        [SerializeField, Min(1f)] float spawnPeriodSeconds = 8f;
        [Tooltip("Seconds between rings with the Time level-5 upgrade — the 'more rings' half " +
                 "of Wake Highway.")]
        [SerializeField, Min(0.5f)] float spawnPeriodAtTime5 = 4f;

        [Header("The surge (what threading a ring pays)")]
        [Tooltip("Forward velocity added to a rider, u/s, at base Time.")]
        [SerializeField, Min(1f)] float surgeSpeed = 60f;
        [Tooltip("Rider surge with the Time level-5 upgrade.")]
        [SerializeField, Min(1f)] float surgeSpeedAtTime5 = 90f;
        [Tooltip("Seconds the surge displacement plays out over.")]
        [SerializeField, Min(0.1f)] float surgeSeconds = 1.5f;
        [Tooltip("Seconds before the SAME vessel can be paid by the SAME ring again.")]
        [SerializeField, Min(0.5f)] float perVesselRideCooldown = 3f;

        [Header("Upkeep")]
        [Tooltip("The switch retires itself once more than this fraction of its ring's prisms " +
                 "are gone (grazed, destroyed, recycled) — a booster with no visible ring " +
                 "would be a lie. The prisms themselves are ordinary conserved mass and are " +
                 "never culled by this.")]
        [SerializeField, Range(0.1f, 1f)] float retireBelowPrismFraction = 0.5f;

        public int Segments => segments;
        public float RingRadius => ringRadius;
        public Vector3 PrismScale => prismScale;
        public float BehindOffset => behindOffset;
        public float SpawnPeriodSeconds => spawnPeriodSeconds;
        public float SpawnPeriodAtTime5 => spawnPeriodAtTime5;
        public float SurgeSpeed => surgeSpeed;
        public float SurgeSpeedAtTime5 => surgeSpeedAtTime5;
        public float SurgeSeconds => surgeSeconds;
        public float PerVesselRideCooldown => perVesselRideCooldown;
        public float RetireBelowPrismFraction => retireBelowPrismFraction;
    }
}
