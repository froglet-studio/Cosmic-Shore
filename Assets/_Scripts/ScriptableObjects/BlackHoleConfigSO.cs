using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for BLACK HOLES (<c>BlackHoleRegistry</c>, <c>BlackHoleGravityField</c>,
    /// <c>BlackHoleWarp</c>, <c>PrismGravityWarp.hlsl</c>, Docs/BLACK_HOLE.md).
    ///
    /// A black hole is spawned with one number — its STRENGTH — and everything else about it is
    /// derived here, so the console's <c>blackhole spawn 10</c> and a designer's asset edit reach
    /// the same physics. Strength scales both the gravitational parameter (<c>GM</c>, what pulls)
    /// and the event horizon (<c>r_s</c>, what swallows), the two numbers the pseudo-Newtonian
    /// potential needs; the influence radius (how far out mass is simulated at all) follows from
    /// those plus the acceleration floor below which a pull is not worth a body.
    ///
    /// The field moves mass (live gameplay data — the movers contract) and the WARP bends what is
    /// drawn (a §4.7 global uniform, photons only); the two halves are tuned separately below and
    /// the second never changes anything the first simulates.
    ///
    /// Place the asset at <c>Resources/BlackHoleConfig</c>. With no asset the defaults below
    /// apply, so a spawn works with nothing authored.
    /// </summary>
    [CreateAssetMenu(fileName = "BlackHoleConfig", menuName = "ScriptableObjects/Environment/Black Hole Config")]
    public class BlackHoleConfigSO : ScriptableObject
    {
        [Header("Physics (per unit of strength)")]
        [Tooltip("Gravitational parameter GM per unit of strength, in world-units^3 / s^2. A hole of " +
                 "strength S pulls with GM = S x this. At the default, strength 10 gives a circular " +
                 "orbital speed of ~45 u/s at 100 u and an escape speed of ~70 u/s there — a cruising " +
                 "Squirrel (54 u/s) is caught, a boosting Dolphin is not.")]
        [Min(0f)]
        [SerializeField] float gmPerStrength = 20000f;

        [Tooltip("Event-horizon radius per unit of strength, world units (physically r_s is linear in " +
                 "mass). Anything whose centre crosses the horizon is CAPTURED: a prism is consumed " +
                 "into the singularity, a vessel is held at the ceiling pull. The horizon is also the " +
                 "radius of the black sphere drawn at the hole.")]
        [Min(0.01f)]
        [SerializeField] float horizonPerStrength = 2f;

        [Tooltip("Minimum event-horizon radius, world units, so a feeble hole still has a visible " +
                 "sphere and a finite singularity to integrate against.")]
        [Min(0.01f)]
        [SerializeField] float minHorizonRadius = 1.5f;

        [Tooltip("Acceleration floor, u/s^2: the influence radius is where the hole's pull falls to " +
                 "this. Beyond it mass is not simulated (it would barely move and the body budget is " +
                 "better spent nearer the hole). Lower = larger reach, more bodies.")]
        [Min(0.0001f)]
        [SerializeField] float influenceAccelerationFloor = 0.6f;

        [Tooltip("Hard ceiling on the influence radius, world units, whatever the strength says. Bounds " +
                 "the spatial query and the body count for an operator who types strength 10000.")]
        [Min(1f)]
        [SerializeField] float maxInfluenceRadius = 900f;

        [Tooltip("Frame dragging (Lense-Thirring): how fast the hole's rotating spacetime carries mass " +
                 "around it, as a fraction of the local circular-orbit angular rate. 0 = a static hole " +
                 "(everything at rest falls straight in); 1 = the dragged frame co-rotates at orbital " +
                 "speed, so mass is swept into orbits and the far field keeps turning instead of " +
                 "plunging. Between the two mass spirals in.")]
        [Range(0f, 1.5f)]
        [SerializeField] float frameDragging = 0.8f;

        [Tooltip("How quickly a body's velocity is coupled to the dragged frame, 1/s. The drag is a " +
                 "viscous coupling toward the frame's velocity (not an impulse), so this is the " +
                 "relaxation rate. 0 disables the coupling even with frameDragging set.")]
        [Min(0f)]
        [SerializeField] float frameDragCoupling = 0.6f;

        [Tooltip("Damping applied to a body's velocity once it is OUTSIDE every hole's influence, 1/s. " +
                 "A body a hole has flung clear decelerates at this rate and is released from the " +
                 "simulation when it is slower than Release Speed — so mass settles rather than " +
                 "coasting across the arena forever.")]
        [Min(0f)]
        [SerializeField] float releaseDamping = 1.5f;

        [Tooltip("Speed, u/s, below which a body outside every influence sphere is released back to " +
                 "static mass.")]
        [Min(0.01f)]
        [SerializeField] float releaseSpeed = 0.75f;

        [Tooltip("Substep ceiling for the integrator. A body near the horizon is stepped up to this " +
                 "many times per frame so the pseudo-Newtonian pole is integrated rather than jumped.")]
        [Range(1, 16)]
        [SerializeField] int maxSubsteps = 8;

        [Header("Budgets")]
        [Tooltip("Hard ceiling on prism bodies under gravity across every live hole, per frame. The " +
                 "nearest prisms to a hole win. This is the whole per-frame cost of the field: one " +
                 "Burst job over this many transforms, one bulk render write, one bulk index write.")]
        [Min(0)]
        [SerializeField] int maxBodies = 6000;

        [Tooltip("How many black holes may be live at once (console spawns past this are refused). " +
                 "Mirrors PRISM_GRAVITY_WARP_SLOTS in PrismGravityWarp.hlsl — change both together.")]
        [Range(1, 4)]
        [SerializeField] int maxBlackHoles = 4;

        [Tooltip("Seconds between admission sweeps (the spatial query that finds new prisms inside " +
                 "an influence sphere). Bodies already admitted integrate every frame regardless.")]
        [Min(0.02f)]
        [SerializeField] float admissionInterval = 0.1f;

        [Header("Vessels")]
        [Tooltip("Master switch for pulling VESSELS. The pull goes through " +
                 "VesselTransformer.ModifyVelocity, so a vessel's own engine still works against it " +
                 "— a vessel faster than the local escape speed gets away, bent; one slower is drawn " +
                 "in. Applied only on the machine that drives the vessel (owner or non-networked).")]
        [SerializeField] bool pullVessels = true;

        [Tooltip("Scales the gravitational acceleration a vessel feels relative to a prism (1 = the " +
                 "same physics). Below 1 makes holes a hazard rather than a trap.")]
        [Min(0f)]
        [SerializeField] float vesselPullScale = 1f;

        [Tooltip("Ceiling on the accumulated gravitational velocity a vessel carries, u/s. " +
                 "VesselTransformer clamps its whole velocity-shift channel at 100 u/s anyway; this " +
                 "keeps the hole's share below that so knockbacks still register on a falling ship.")]
        [Min(0f)]
        [SerializeField] float maxVesselPullSpeed = 90f;

        [Header("Warp (photons only — Docs/PRISM_ANIMATION.md §4.7)")]
        [Tooltip("Master switch for the GPU warp. Off publishes an empty bank, which makes the shader's " +
                 "first branch return the untouched vertex.")]
        [SerializeField] bool warpEnabled = true;

        [Tooltip("The tidal STRAIN at the horizon: the fraction of its distance a vertex AT the " +
                 "horizon is pulled toward the singularity. Near faces are pulled more than far faces, " +
                 "so a prism elongates along the radial and squeezes tangentially — spaghettification. " +
                 "Clamped below 1: at 1 the horizon maps onto the centre and the map folds.")]
        [Range(0f, 0.95f)]
        [SerializeField] float warpStrength = 0.6f;

        [Tooltip("How far beyond the horizon the warp reaches, as a multiple of the horizon radius. At " +
                 "that distance the displacement, its derivative and the normal correction are all " +
                 "exactly zero — it is the width of the warped shell, not a cutoff.")]
        [Min(1.01f)]
        [SerializeField] float warpReachMultiplier = 6f;

        [Tooltip("Shaping power of the warp's falloff. 1 is a broad gradient from the horizon to the " +
                 "reach; larger concentrates the bending at the horizon with a long flat tail. " +
                 "Floored at 1, where the falloff's derivative stops being finite at the far edge.")]
        [Range(1f, 6f)]
        [SerializeField] float warpExponent = 2f;

        [Tooltip("Seconds the warp takes to reach full strength after a spawn, and to let go after a " +
                 "despawn. A bare on/off would snap every vertex in the shell on one frame.")]
        [Min(0f)]
        [SerializeField] float warpEaseSeconds = 0.5f;

        [Header("Warp geometry residency")]
        [Tooltip("Quads per face axis on the high-poly prism the warp swaps in near a hole (the " +
                 "cradle's HighPolyPrismMesh, shared with it): 12 is 1,728 triangles against the " +
                 "authored 24. A bend is only as smooth as the surface it moves.")]
        [Range(2, 32)]
        [SerializeField] int warpSubdivision = 12;

        [Tooltip("Hard ceiling on how many prisms may hold the high-poly mesh across every hole. The " +
                 "nearest prisms to a horizon win; everything else warps at the authored mesh's " +
                 "resolution — coarse, never wrong.")]
        [Min(0)]
        [SerializeField] int warpMaxResidentPrisms = 32;

        [Tooltip("Extra world units beyond the warp reach at which a prism becomes resident, so the " +
                 "mesh swap happens strictly OUTSIDE the volume the warp can move anything.")]
        [Min(0f)]
        [SerializeField] float warpResidencyMargin = 3f;

        [Header("Lens (photons only — what the player sees of the hole, Docs/BLACK_HOLE.md §5.1)")]
        [Tooltip("Master switch for the gravitational-lens visual. Off draws the plain black sphere " +
                 "instead. The lens bends the opaque scene behind the hole, so while it is on the " +
                 "main camera's opaque and depth textures are switched on (only while a hole is live).")]
        [SerializeField] bool lensEnabled = true;

        [Tooltip("How far around the hole the bending is drawn, in horizon radii. Light passing at b " +
                 "is really deflected by ~2/b — it never reaches zero — so the bend is faded out over " +
                 "the outer part of this radius (Lens Fade Start). Larger reaches farther, costs more " +
                 "screen pixels.")]
        [Range(6f, 120f)]
        [SerializeField] float lensRadiusMultiplier = 30f;

        [Tooltip("Where the bend starts fading back to the straight ray, as a fraction of the lens " +
                 "radius. Inside it the ray trace is exact.")]
        [Range(0.1f, 0.95f)]
        [SerializeField] float lensFadeStart = 0.55f;

        [Tooltip("Ray-march step budget per pixel. Rays near the photon sphere (1.5 horizon radii) " +
                 "need the most; 128 traces one full loop around it.")]
        [Range(16, 192)]
        [SerializeField] int lensSteps = 128;

        [Header("Accretion disc (photons only — fed by what the hole eats)")]
        [Tooltip("The disc's inner edge, horizon radii. 3 is the innermost stable orbit (ISCO): gas " +
                 "inside it plunges, which is the dark gap between the disc and the shadow.")]
        [Range(1.5f, 10f)]
        [SerializeField] float diskInnerMultiplier = 3f;

        [Tooltip("The disc's outer edge, horizon radii.")]
        [Range(4f, 60f)]
        [SerializeField] float diskOuterMultiplier = 14f;

        [Tooltip("Disc density with nothing being eaten — a starving hole's faint ring. 0 = no disc " +
                 "until the hole has consumed something.")]
        [Range(0f, 2f)]
        [SerializeField] float diskBaseDensity = 0.12f;

        [Tooltip("Density each consumed prism adds to the disc. The disc a hole shows is the mass it " +
                 "has been FED: eating forms it in real time, starving lets it fade.")]
        [Range(0f, 0.5f)]
        [SerializeField] float diskFeedPerCapture = 0.03f;

        [Tooltip("Ceiling on the fed density.")]
        [Range(0f, 4f)]
        [SerializeField] float diskFeedMax = 1.6f;

        [Tooltip("Seconds for the fed density to halve once the hole stops eating.")]
        [Min(0.1f)]
        [SerializeField] float diskFeedHalfLife = 6f;

        [Tooltip("Disc emission brightness (HDR). Above ~1 the hot side blooms.")]
        [Range(0f, 20f)]
        [SerializeField] float diskBrightness = 4f;

        [Tooltip("Temperature of the hottest ring, kelvin. ~4,000 is orange, ~6,500 warm white (the " +
                 "Interstellar look), ~10,000 white-hot, ~20,000 blue-white. Real accretion discs are far " +
                 "hotter than 10,000 K, so white to blue-white is the realistic range. The outer disc is " +
                 "cooler, and the Doppler shift moves each side of the disc from here.")]
        [Range(1500f, 30000f)]
        [SerializeField] float diskPeakTemperature = 10000f;

        [Tooltip("How much of the relativistic Doppler shift and beaming to apply, 0..1. At 1 the side " +
                 "of the disc turning toward the camera is bluer and much brighter than the side " +
                 "turning away — the lopsided glow of every real black hole image.")]
        [Range(0f, 1f)]
        [SerializeField] float diskDoppler = 1f;

        [Tooltip("How fast the gas pattern turns, as a multiple of game time. The rotation is " +
                 "Keplerian (inner gas laps the outer), in horizon-crossing time units.")]
        [Range(0f, 50f)]
        [SerializeField] float diskSpinSpeed = 6f;

        [Tooltip("Scale of the gas streaks in the disc.")]
        [Range(0.2f, 4f)]
        [SerializeField] float diskNoiseScale = 1f;

        public bool LensEnabled => lensEnabled;
        public float LensRadiusMultiplier => Mathf.Clamp(lensRadiusMultiplier, 6f, 120f);
        public float LensFadeStart => Mathf.Clamp(lensFadeStart, 0.1f, 0.95f);
        public int LensSteps => Mathf.Clamp(lensSteps, 16, 192);
        public float DiskInnerMultiplier => Mathf.Clamp(diskInnerMultiplier, 1.5f, 10f);
        public float DiskOuterMultiplier => Mathf.Max(DiskInnerMultiplier + 0.5f, Mathf.Clamp(diskOuterMultiplier, 4f, 60f));
        public float DiskBaseDensity => Mathf.Clamp(diskBaseDensity, 0f, 2f);
        public float DiskFeedPerCapture => Mathf.Clamp(diskFeedPerCapture, 0f, 0.5f);
        public float DiskFeedMax => Mathf.Clamp(diskFeedMax, 0f, 4f);
        public float DiskFeedHalfLife => Mathf.Max(0.1f, diskFeedHalfLife);
        public float DiskBrightness => Mathf.Clamp(diskBrightness, 0f, 20f);
        public float DiskPeakTemperature => Mathf.Clamp(diskPeakTemperature, 1500f, 30000f);
        public float DiskDoppler => Mathf.Clamp01(diskDoppler);
        public float DiskSpinSpeed => Mathf.Clamp(diskSpinSpeed, 0f, 50f);
        public float DiskNoiseScale => Mathf.Clamp(diskNoiseScale, 0.2f, 4f);

        public float GmPerStrength => Mathf.Max(0f, gmPerStrength);
        public float HorizonPerStrength => Mathf.Max(0.01f, horizonPerStrength);
        public float MinHorizonRadius => Mathf.Max(0.01f, minHorizonRadius);
        public float InfluenceAccelerationFloor => Mathf.Max(0.0001f, influenceAccelerationFloor);
        public float MaxInfluenceRadius => Mathf.Max(1f, maxInfluenceRadius);
        public float FrameDragging => Mathf.Clamp(frameDragging, 0f, 1.5f);
        public float FrameDragCoupling => Mathf.Max(0f, frameDragCoupling);
        public float ReleaseDamping => Mathf.Max(0f, releaseDamping);
        public float ReleaseSpeed => Mathf.Max(0.01f, releaseSpeed);
        public int MaxSubsteps => Mathf.Clamp(maxSubsteps, 1, 16);
        public int MaxBodies => Mathf.Max(0, maxBodies);
        public int MaxBlackHoles => Mathf.Clamp(maxBlackHoles, 1, 4);
        public float AdmissionInterval => Mathf.Max(0.02f, admissionInterval);
        public bool PullVessels => pullVessels;
        public float VesselPullScale => Mathf.Max(0f, vesselPullScale);
        public float MaxVesselPullSpeed => Mathf.Max(0f, maxVesselPullSpeed);
        public bool WarpEnabled => warpEnabled;
        public float WarpStrength => Mathf.Clamp(warpStrength, 0f, 0.95f);
        public float WarpReachMultiplier => Mathf.Max(1.01f, warpReachMultiplier);
        public float WarpExponent => Mathf.Clamp(warpExponent, 1f, 6f);
        public float WarpEaseSeconds => Mathf.Max(0f, warpEaseSeconds);
        public int WarpSubdivision => Mathf.Clamp(warpSubdivision, 2, 32);
        public int WarpMaxResidentPrisms => Mathf.Max(0, warpMaxResidentPrisms);
        public float WarpResidencyMargin => Mathf.Max(0f, warpResidencyMargin);

        /// <summary>Gravitational parameter of a hole of the given strength.</summary>
        public float GM(float strength) => Mathf.Max(0f, strength) * GmPerStrength;

        /// <summary>Event-horizon radius of a hole of the given strength.</summary>
        public float HorizonRadius(float strength) =>
            Mathf.Max(MinHorizonRadius, Mathf.Max(0f, strength) * HorizonPerStrength);

        /// <summary>
        /// Radius at which the pull falls to <see cref="InfluenceAccelerationFloor"/>, capped at
        /// <see cref="MaxInfluenceRadius"/> and never inside the horizon.
        /// </summary>
        public float InfluenceRadius(float strength)
        {
            float rs = HorizonRadius(strength);
            float gm = GM(strength);
            // Paczynski-Wiita: a = GM / (d - rs)^2  =>  d = rs + sqrt(GM / a_floor).
            float d = rs + Mathf.Sqrt(gm / InfluenceAccelerationFloor);
            return Mathf.Clamp(d, rs * 1.5f, MaxInfluenceRadius);
        }

        /// <summary>World units the warp reaches beyond a hole's horizon.</summary>
        public float WarpReach(float strength) => HorizonRadius(strength) * (WarpReachMultiplier - 1f);

        /// <summary>
        /// The shape the shader and the integrator can actually run: a positive horizon, a reach
        /// beyond it, a strain below the fold. An insane asset degrades to "off" rather than to a
        /// divide by zero.
        /// </summary>
        public bool IsSane =>
            HorizonPerStrength > 0f && MinHorizonRadius > 0f &&
            WarpReachMultiplier > 1f && WarpStrength < 1f && WarpExponent >= 1f &&
            InfluenceAccelerationFloor > 0f &&
            // The lens must enclose the disc, or the disc's outer rim is cut off by the billboard.
            LensRadiusMultiplier > DiskOuterMultiplier && DiskOuterMultiplier > DiskInnerMultiplier;
    }
}
