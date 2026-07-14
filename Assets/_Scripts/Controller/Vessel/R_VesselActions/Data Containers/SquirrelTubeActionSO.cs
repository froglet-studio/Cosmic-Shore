using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Squirrel "Oak Trunk" tube ability. Pressing the ability trigger lays an <b>arrangement of
    /// prisms</b> straight out in front of the vessel: a long tube of thick danger prisms along the
    /// nose / flight direction so a Squirrel flying straight rockets through the hollow centre and
    /// skims the whole danger wall (10x boost energy per danger skim) while it obstructs everyone
    /// else — danger prisms slam any vessel body that touches them, friendly fire included (locked
    /// design). No preview; it just places.
    ///
    /// The placement is led by the vessel's speed (<see cref="LeadSeconds"/>) so it appears a fixed
    /// travel-time ahead. Every ring is laid through the shared <see cref="BoostRingBuilder"/> (the
    /// same primitive behind the omnicrystal and joust rings): real conserved mass from the dedicated
    /// <b>Boost</b> prism POOL, blooming in (continuity law) with a FULL-SIZE collider from frame 0,
    /// registering with <see cref="PrismSpatialIndex"/>, and only ever removed by an active force
    /// (skim/ability/fauna) — never a timer. See <c>SQUIRREL_TUBE.md</c>.
    /// Everything below is designer-tunable; the <see cref="Radius"/> in particular must be tuned
    /// in-editor so a level-0 Space Skimmer sphere reaches the ring while the vessel body clears it.
    /// </summary>
    [CreateAssetMenu(fileName = "SquirrelTubeAction", menuName = "ScriptableObjects/Vessel Actions/Squirrel Tube")]
    public class SquirrelTubeActionSO : ShipActionSO
    {
        [Header("Prism")]
        [Tooltip("Lay the wall as danger prisms (slam on body contact, 10x skim energy).")]
        [SerializeField] private bool danger = true;

        [Header("Tube Geometry")]
        [Tooltip("Radius of the prism ring in world units. Tune so a level-0 Space skimmer sphere " +
                 "reaches the ring from the axis while the vessel body flies clear of it.")]
        [SerializeField] private float radius = 8f;

        [Tooltip("Prisms per ring. More segments = a denser, more skimmable wall.")]
        [SerializeField] private int segments = 8;

        [Tooltip("Number of rings along the axis. Length = (rings - 1) * ringSpacing. Make it long.")]
        [SerializeField] private int rings = 44;

        [Tooltip("Distance between rings along the tube axis (world units).")]
        [SerializeField] private float ringSpacing = 6f;

        [Tooltip("Uniform world scale each wall prism grows to. Larger = thicker trunk.")]
        [SerializeField] private float prismScale = 6f;

        [Header("Placement")]
        [Tooltip("Seconds of travel ahead of the vessel the tube mouth forms — the offset is " +
                 "speed * leadSeconds, so a faster Squirrel places it further out.")]
        [SerializeField] private float leadSeconds = 1f;

        [Tooltip("Minimum offset ahead of the vessel (world units), so the tube never forms on top " +
                 "of the vessel when slow or stopped.")]
        [SerializeField] private float forwardOffset = 24f;

        [Header("Spawn / Cooldown")]
        [Tooltip("Wall prisms laid per frame, to spread the spawn spike.")]
        [SerializeField] private int spawnPerFrame = 24;

        [Tooltip("Seconds before the ability can be used again after it forms. Keep it long.")]
        [SerializeField] private float cooldown = 20f;

        public bool Danger => danger;
        public float Radius => radius;
        public int Segments => Mathf.Max(3, segments);
        public int Rings => Mathf.Max(1, rings);
        public float RingSpacing => Mathf.Max(0.01f, ringSpacing);
        public float PrismScale => Mathf.Max(0.01f, prismScale);
        public float LeadSeconds => Mathf.Max(0f, leadSeconds);
        public float ForwardOffset => forwardOffset;
        public int SpawnPerFrame => Mathf.Max(1, spawnPerFrame);
        public float Cooldown => Mathf.Max(0f, cooldown);

        /// <summary>Total axial length the tube spans, from the first ring to the last.</summary>
        public float Length => (Rings - 1) * RingSpacing;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SquirrelTubeActionExecutor>()?.Begin(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<SquirrelTubeActionExecutor>()?.Commit(this, vesselStatus);
    }
}
