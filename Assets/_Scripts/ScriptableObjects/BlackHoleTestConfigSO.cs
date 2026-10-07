using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tunables for the black hole test scene (<c>BlackHoleTestHarness</c>, Docs/BLACK_HOLE.md §7):
    /// the prism field it lays, the holes its buttons spawn, and the camera. Every knob the rig
    /// exposes lives here rather than on the MonoBehaviour (CLAUDE.md ▸ ScriptableObject Config
    /// Separation), so a tuning pass is an asset edit and the scene stays regenerable by the
    /// setup tool. The physics itself is NOT here — that is <see cref="BlackHoleConfigSO"/>, the
    /// same asset every scene reads, so what this rig shows is what the game does.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BlackHoleTestConfig",
        menuName = "ScriptableObjects/Testing/Black Hole Test Config")]
    public class BlackHoleTestConfigSO : ScriptableObject
    {
        public enum FieldShape
        {
            Cuboid = 0,
            Spheroid = 1,
        }

        [Header("Field")]
        [Tooltip("Shape of the prism field: a cuboid lattice, or the spheroid inscribed in it (lattice " +
                 "sites outside the ellipsoid are skipped). Both are centred on the world origin.")]
        [SerializeField] private FieldShape defaultShape = FieldShape.Spheroid;

        [Tooltip("Lattice sites along each axis before the shape cut. 25^3 = 15,625 sites; the " +
                 "inscribed spheroid keeps ~52% of them.")]
        [SerializeField] private Vector3Int defaultCounts = new(25, 25, 25);

        [Tooltip("Centre-to-centre pitch per axis, world units. Extent along an axis = (count - 1) * gap.")]
        [SerializeField] private Vector3 defaultGaps = new(24f, 24f, 24f);

        [Tooltip("Random offset per prism as a fraction of the pitch (0 = a perfect lattice, 0.5 = " +
                 "sites wander half a gap). A little jitter makes the orbits read as mass rather " +
                 "than as a grid rotating rigidly.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float jitter = 0.25f;

        [Tooltip("Random rotation per prism (on) or axis-aligned (off).")]
        [SerializeField] private bool randomRotation = true;

        [Tooltip("Hard ceiling on the prism count a spawn may request.")]
        [SerializeField] private int maxTotalPrisms = 60000;

        [Tooltip("How many prisms PrismTrailBuilder.LayBatched lays per frame.")]
        [SerializeField] private int prismsPerFrame = 200;

        [Header("Prisms")]
        [Tooltip("Prism prefab laid at every site. Defaults to the Dolphin prism, the grid rig's choice.")]
        [SerializeField] private Prism prismPrefab;

        [Tooltip("Domain the field is laid in.")]
        [SerializeField] private Domains fieldDomain = Domains.Jade;

        [Tooltip("Per-prism target scale. Leave at zero to keep the prefab's authored scale.")]
        [SerializeField] private Vector3 prismScale = Vector3.zero;

        [Tooltip("Seconds the Clear button takes to suction the whole field toward the origin before " +
                 "freeing it — the grid rig's sanctioned continuity transition.")]
        [SerializeField] private float clearSeconds = 0.35f;

        [Header("Black holes")]
        [Tooltip("Strength of the hole the 'Hole at centre' button spawns.")]
        [Min(0f)]
        [SerializeField] private float centreStrength = 10f;

        [Tooltip("Strength of the hole the 'Fly-through' button drives across the field.")]
        [Min(0f)]
        [SerializeField] private float flyStrength = 8f;

        [Tooltip("Speed, u/s, the fly-through hole crosses the field at (along +X, through the centre).")]
        [Min(0.1f)]
        [SerializeField] private float flySpeed = 60f;

        [Tooltip("World units outside the field's X extent the fly-through hole starts at and is " +
                 "despawned past.")]
        [Min(0f)]
        [SerializeField] private float flyMargin = 150f;

        [Tooltip("Spin axis for holes spawned by this rig. +Z (toward the camera) makes the frame " +
                 "dragging swirl the field in the camera's own plane, where it reads best.")]
        [SerializeField] private Vector3 spinAxis = Vector3.forward;

        [Header("Camera")]
        [Tooltip("Camera distance from the origin at zoom = 0. Never zero.")]
        [SerializeField] private float nearDistance = 60f;

        [Tooltip("Camera distance at zoom = 1, as a multiple of the field's largest extent.")]
        [SerializeField] private float farDistanceMultiplier = 1.4f;

        [Tooltip("Zoom slider value the scene starts at.")]
        [Range(0f, 1f)]
        [SerializeField] private float defaultZoom = 0.75f;

        public FieldShape DefaultShape => defaultShape;
        public Vector3Int DefaultCounts => new(
            Mathf.Max(1, defaultCounts.x), Mathf.Max(1, defaultCounts.y), Mathf.Max(1, defaultCounts.z));
        public Vector3 DefaultGaps => new(
            Mathf.Max(0.01f, defaultGaps.x), Mathf.Max(0.01f, defaultGaps.y), Mathf.Max(0.01f, defaultGaps.z));
        public float Jitter => Mathf.Clamp(jitter, 0f, 0.5f);
        public bool RandomRotation => randomRotation;
        public int MaxTotalPrisms => Mathf.Max(1, maxTotalPrisms);
        public int PrismsPerFrame => Mathf.Max(1, prismsPerFrame);
        public Prism PrismPrefab => prismPrefab;
        public Domains FieldDomain => fieldDomain;
        public Vector3 PrismScale => prismScale;
        public float ClearSeconds => Mathf.Max(0f, clearSeconds);
        public float CentreStrength => Mathf.Max(0f, centreStrength);
        public float FlyStrength => Mathf.Max(0f, flyStrength);
        public float FlySpeed => Mathf.Max(0.1f, flySpeed);
        public float FlyMargin => Mathf.Max(0f, flyMargin);
        public Vector3 SpinAxis => spinAxis.sqrMagnitude > 1e-6f ? spinAxis.normalized : Vector3.forward;
        public float NearDistance => Mathf.Max(1f, nearDistance);
        public float FarDistanceMultiplier => Mathf.Max(0.1f, farDistanceMultiplier);
        public float DefaultZoom => defaultZoom;
    }
}
