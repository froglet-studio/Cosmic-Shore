using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Squirrel "Oak Trunk" tube ability. Hold-to-preview / release-to-form, the same
    /// press→ghost, release→commit shape as the Dolphin's <see cref="DeployTeamCrystalActionSO"/>,
    /// but the committed object is an <b>arrangement of prisms</b> instead of a single crystal:
    /// a long tube of thick danger prisms laid along the vessel's forward axis so a Squirrel
    /// flying straight rockets through the hollow centre and skims the whole danger wall (10x
    /// boost energy per danger skim) while it obstructs everyone else — danger prisms slam any
    /// vessel body that touches them, friendly fire included (locked design).
    ///
    /// The tube is real conserved mass laid through the canonical <see cref="PrismTrailBuilder"/>
    /// path, so it blooms in (continuity law), registers with <see cref="PrismSpatialIndex"/>,
    /// and is only ever removed by an active force (skim/ability/fauna) — never a timer.
    /// Everything below is designer-tunable in the SO; the geometry defaults are a starting point
    /// and the <see cref="Radius"/> in particular must be tuned in-editor so a level-0 Space
    /// Skimmer sphere reaches the ring while the vessel body clears it.
    /// </summary>
    [CreateAssetMenu(fileName = "SquirrelTubeAction", menuName = "ScriptableObjects/Vessel Actions/Squirrel Tube")]
    public class SquirrelTubeActionSO : ShipActionSO
    {
        [Header("Prism")]
        [Tooltip("Prism prefab the tube wall is built from (the Squirrel prism).")]
        [SerializeField] private Prism prism;

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

        [Tooltip("How far ahead of the vessel the tube mouth forms (world units).")]
        [SerializeField] private float forwardOffset = 24f;

        [Header("Preview")]
        [Tooltip("Optional translucent material for the hold preview. If null a runtime unlit " +
                 "transparent fallback is created.")]
        [SerializeField] private Material previewMaterial;

        [Tooltip("Tint used for the runtime fallback preview material (danger-warning colour).")]
        [SerializeField] private Color previewColor = new Color(1f, 0.45f, 0.1f, 0.28f);

        [Tooltip("Seconds to fade the preview in when the trigger is pressed.")]
        [SerializeField] private float previewFadeInSeconds = 0.15f;

        [Header("Spawn / Cooldown")]
        [Tooltip("Wall prisms laid per frame on release, to spread the spawn spike.")]
        [SerializeField] private int spawnPerFrame = 24;

        [Tooltip("Seconds before the ability can be used again after it forms. Keep it long.")]
        [SerializeField] private float cooldown = 20f;

        public Prism Prism => prism;
        public bool Danger => danger;
        public float Radius => radius;
        public int Segments => Mathf.Max(3, segments);
        public int Rings => Mathf.Max(1, rings);
        public float RingSpacing => Mathf.Max(0.01f, ringSpacing);
        public float PrismScale => Mathf.Max(0.01f, prismScale);
        public float ForwardOffset => forwardOffset;
        public Material PreviewMaterial => previewMaterial;
        public Color PreviewColor => previewColor;
        public float PreviewFadeInSeconds => Mathf.Max(0f, previewFadeInSeconds);
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
