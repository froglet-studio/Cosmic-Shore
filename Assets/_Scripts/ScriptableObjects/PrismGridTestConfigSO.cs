using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tunables for the prism-grid explosion test scene (<c>PrismGridExplosionHarness</c>).
    ///
    /// Per CLAUDE.md ▸ ScriptableObject Config Separation, every knob the rig exposes lives
    /// here rather than on the MonoBehaviour, so a tuning pass is an asset edit and the scene
    /// itself stays disposable/regenerable by the setup tool.
    /// </summary>
    [CreateAssetMenu(
        fileName = "PrismGridTestConfig",
        menuName = "ScriptableObjects/Testing/Prism Grid Test Config")]
    public class PrismGridTestConfigSO : ScriptableObject
    {
        [Header("Grid")]
        [Tooltip("Default prism count along each axis: X = length, Y = breadth, Z = height. " +
                 "The lattice is always centred on the world origin. Benchmark spec: 47^3 = " +
                 "103,823 — the ~100k CUBE, odd-sided so an exact centre prism exists on every " +
                 "face (the inscribed blast's end condition is 'face-centre prisms destroyed').")]
        [SerializeField] private Vector3Int defaultCounts = new(47, 47, 47);

        [Tooltip("Default gap between adjacent prisms PER AXIS, as CENTRE-TO-CENTRE pitch in world " +
                 "units. Cuboid extent along an axis = (count - 1) * gap. Unequal gaps make the " +
                 "inscribed blast bind to the tightest axis.")]
        [SerializeField] private Vector3 defaultGaps = new(30f, 30f, 30f);

        [Tooltip("Hard ceiling on countX * countY * countZ. A spawn request above this is rejected " +
                 "before any prism is laid, so a fat-fingered 100^3 cannot lock up the editor.")]
        [SerializeField] private int maxTotalPrisms = 150000;

        [Tooltip("How many prisms PrismTrailBuilder.LayBatched lays per frame. Prism itself also " +
                 "throttles completions per frame, so this stacks on top of that budget.")]
        [SerializeField] private int prismsPerFrame = 200;

        [Header("Prisms")]
        [Tooltip("Prism prefab laid at every lattice site. Defaults to the Dolphin prism to match " +
                 "the Dolphin blast this scene fires.")]
        [SerializeField] private Prism prismPrefab;

        [Tooltip("Domain the lattice is laid in. MUST differ from the explosion domain, or the blast " +
                 "leaves it untouched (AOEExplosion.prefab ships affectSelf = false).")]
        [SerializeField] private Domains gridDomain = Domains.Jade;

        [Tooltip("Per-prism target scale. Leave at zero to keep the prefab's authored scale.")]
        [SerializeField] private Vector3 prismScale = Vector3.zero;

        [Tooltip("Seconds the Clear button takes to suction the whole lattice toward the origin " +
                 "before freeing it. Matches Microscene.RecycleAsync's sanctioned continuity " +
                 "transition — nothing pops out of existence.")]
        [SerializeField] private float clearSeconds = 0.35f;

        [Header("Explosion")]
        [Tooltip("Explosion prefab. The base spherical AOEExplosion is used rather than the Dolphin's " +
                 "AOEConicExplosion because the cone hard-requires a live vessel and damages prisms " +
                 "through physics triggers only, bypassing the Burst PrismSpatialIndex path.")]
        [SerializeField] private AOEExplosion explosionPrefab;

        [Tooltip("Explosion max scale. 800 is the Dolphin's giant blast value, the largest of any " +
                 "vessel (see DolphinVesselExplosionByCrystalEffect). Blast radius reaches " +
                 "0.5 * maxScale world units. IGNORED while Fit Blast To Lattice is on.")]
        [SerializeField] private float maxScale = 800f;

        [Tooltip("Derive the blast's final radius from the CURRENT lattice instead of Max Scale: " +
                 "the explosion ends INSCRIBED — the wavefront's last overlap sphere reaches the " +
                 "centre of each (nearest) cube face, destroying the face-centre prisms while the " +
                 "edges and corners survive. This is the benchmark's spec'd end condition.")]
        [SerializeField] private bool fitBlastToLattice = true;

        [Tooltip("Wavefront expansion speed in RADIUS world-units per second. 0 keeps the explosion " +
                 "prefab's authored duration (AOEExplosion ships 2s — the wavefront then crosses any " +
                 "lattice in 2s regardless of size, i.e. bigger blasts sweep FASTER). A non-zero " +
                 "value pins the physical expansion rate instead: duration = final radius / speed, " +
                 "so doubling the lattice doubles the sweep time.")]
        [SerializeField, Min(0f)] private float explosionSpeed = 0f;

        [Tooltip("Domain the blast belongs to. MUST differ from the grid domain to be destructive.")]
        [SerializeField] private Domains explosionDomain = Domains.Ruby;

        [Tooltip("Optional explosion material override. When empty the prefab's own material is used " +
                 "— AOEExplosion assigns Material to its renderer, so a null here would render the " +
                 "blast invisible.")]
        [SerializeField] private Material overrideMaterial;

        [Header("Safety throttles")]
        [Tooltip("Lift the gameplay safety throttles for the duration of this scene: the per-frame " +
                 "AOE damage budget (PrismSpatialIndex, 48/frame), the per-frame destruction-VFX " +
                 "spawn caps (PrismFactory, 64/frame), and the live-effect pressure model that " +
                 "shortens explosion durations under load. Those guards were sized for the " +
                 "CPU-per-effect era; on the clock path a running effect costs no per-frame CPU, so " +
                 "the benchmark measures the system UNWEAKENED by default. The harness restores " +
                 "every override on destroy — gameplay scenes are never affected.")]
        [SerializeField] private bool liftSafetyThrottles = true;

        [Header("Camera")]
        [Tooltip("Camera distance from the origin at zoom = 0. Never zero: LookAt from exactly the " +
                 "origin is a degenerate zero-length direction.")]
        [SerializeField] private float nearDistance = 50f;

        [Tooltip("Camera distance at zoom = 1, as a multiple of the cuboid's Z extent.")]
        [SerializeField] private float farDistanceMultiplier = 1.5f;

        [Tooltip("Zoom slider value the scene starts at.")]
        [Range(0f, 1f)]
        [SerializeField] private float defaultZoom = 0.6f;

        public Vector3Int DefaultCounts => defaultCounts;
        public Vector3 DefaultGaps => new(
            Mathf.Max(0.01f, defaultGaps.x),
            Mathf.Max(0.01f, defaultGaps.y),
            Mathf.Max(0.01f, defaultGaps.z));
        public int MaxTotalPrisms => maxTotalPrisms;
        public int PrismsPerFrame => Mathf.Max(1, prismsPerFrame);
        public Prism PrismPrefab => prismPrefab;
        public Domains GridDomain => gridDomain;
        public Vector3 PrismScale => prismScale;
        public float ClearSeconds => Mathf.Max(0f, clearSeconds);
        public AOEExplosion ExplosionPrefab => explosionPrefab;
        public float MaxScale => maxScale;
        public bool FitBlastToLattice => fitBlastToLattice;
        public float ExplosionSpeed => Mathf.Max(0f, explosionSpeed);
        public bool LiftSafetyThrottles => liftSafetyThrottles;
        public Domains ExplosionDomain => explosionDomain;
        public Material OverrideMaterial => overrideMaterial;
        public float NearDistance => Mathf.Max(1f, nearDistance);
        public float FarDistanceMultiplier => Mathf.Max(0.1f, farDistanceMultiplier);
        public float DefaultZoom => defaultZoom;

        /// <summary>
        /// Blast radius at full expansion. AOEExplosion scales its SphereCollider (authored
        /// radius 0.5) by MaxScale, so the reachable radius is half the max scale.
        /// </summary>
        public float BlastRadius => maxScale * 0.5f;

        /// <summary>
        /// True when the blast can actually destroy the lattice. Same-domain blasts are inert
        /// because the explosion prefab does not affect its own domain.
        /// </summary>
        public bool DomainsAreDestructive => gridDomain != explosionDomain;
    }
}
