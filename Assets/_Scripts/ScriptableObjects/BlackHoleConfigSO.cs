using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Tuning for BLACK HOLES (<c>BlackHoleRegistry</c>, <c>BlackHoleGravityField</c>,
    /// <c>BlackHoleWarp</c>, <c>PrismGravityWarp.hlsl</c>, Docs/BLACK_HOLE.md).
    ///
    /// A black hole has two numbers: its STRENGTH, which sets the gravitational parameter
    /// (<c>GM</c>, what pulls), and its SIZE, the event-horizon radius (<c>r_s</c>, what swallows
    /// and what the lens draws). A hole spawned with size 0 derives its size from its strength
    /// (<see cref="HorizonRadius(float)"/>), so a strength alone reaches the same physics
    /// as a designer's asset edit; the Stoat's dipole sets size explicitly
    /// (StoatDipoleConfig). The influence radius (how far out mass is
    /// simulated at all) follows from GM and r_s plus the acceleration floor below which a pull is
    /// not worth a body.
    ///
    /// The field moves mass (live gameplay data — the movers contract) and SPAGHETTIFICATION draws
    /// what its tides do to that mass (a §4.7 global uniform, photons only); the two halves are tuned
    /// separately below and the second never changes anything the first simulates.
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

        [Tooltip("The hole's dimensionless SPIN a* (Kerr): 0 = non-rotating (Schwarzschild), 0.998 = the " +
                 "astrophysical limit. A spinning hole drags the local inertial frame around its spin " +
                 "axis at ω = a*·c·r_s²/(2r³) (Lense-Thirring, c² = 2GM/r_s) — a 1/r³ effect, so mass " +
                 "falling from rest comes in nearly radially and winds up only within a few horizon " +
                 "radii. Lasting orbits need angular momentum the mass brings with it (a moving hole, a " +
                 "moving prism), exactly as around a real hole.")]
        [Range(0f, 0.998f)]
        [SerializeField] float spin = 0.9f;

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

        [Header("Spaghettification (photons only — Docs/BLACK_HOLE.md §5)")]
        [Tooltip("Master switch for the tidal stretch. Off publishes an empty bank, which makes the " +
                 "shader's first branch return the untouched vertex.")]
        [SerializeField] bool warpEnabled = true;

        [Tooltip("How soft prisms are to tides, seconds. The stretch is the tidal tensor of general " +
                 "relativity for radial free fall, (GM/r³)·diag(2, −1, −1), acting for this long on a " +
                 "body: log-stretch ε = GM·τ²/r³ along the radial and −ε/2 across it — volume is " +
                 "conserved, the stretch is strongest at the horizon and falls as 1/r³, and a SMALLER " +
                 "hole shreds harder at its horizon than a big one (ε(r_s) ∝ 1/M², as in reality). " +
                 "0 = rigid prisms, no stretch.")]
        [Range(0f, 3f)]
        [SerializeField] float tidalResponseSeconds = 0.9f;

        [Tooltip("Ceiling on how many times longer a prism can be drawn than it is, so a prism at the " +
                 "horizon of a tiny hole is a long needle rather than a line to infinity. The stretch " +
                 "eases into it through a soft minimum — the physics to 1.5% up to half the ceiling — " +
                 "and never clips.")]
        [Range(1.5f, 30f)]
        [SerializeField] float maxTidalStretch = 12f;

        [Tooltip("How much of the prisms' tide a VESSEL's drawn hull feels (its spaghettification falling into a " +
                 "black hole and its reverse leaving a white one). Physical tides at a fixed r/r_s grow as 1/r_s², " +
                 "so a hull passing a small hole would be a needle long before it fell in; this keeps a near pass a " +
                 "hint while the horizon still reaches the full stretch. 0 = off.")]
        [Range(0f, 1f)]
        [SerializeField] float vesselTideScale = 0.08f;

        [Tooltip("How far beyond the horizon the stretch is computed, as a multiple of the horizon " +
                 "radius. The tide is drawn exactly across the inner half of that shell and faded " +
                 "smoothly to zero across the outer half, where the 1/r³ tide is already ≤ 1/43 of the " +
                 "horizon's (at 6) — so no prism beyond pays for it and none pops at the edge.")]
        [Min(1.01f)]
        [SerializeField] float warpReachMultiplier = 6f;

        [Tooltip("Seconds the stretch takes to reach full strength after a spawn, and to let go after a " +
                 "despawn. A bare on/off would snap every prism in the shell on one frame.")]
        [Min(0f)]
        [SerializeField] float warpEaseSeconds = 0.5f;

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

        [Tooltip("Resolution of each of the six faces of the SKY the lens bends — the scene's own skybox " +
                 "(Lighting > Environment > Skybox Material), rendered for rays bent off the screen. " +
                 "Higher is sharper stars at the lens's outer edge; each face costs one skybox draw of " +
                 "this size.")]
        [Range(128, 2048)]
        [SerializeField] int lensSkyResolution = 1024;

        [Tooltip("How many of the sky's six faces are re-rendered each frame, round-robin, so an animated " +
                 "skybox stays in step with the real one. 0 = render once (and again whenever the skybox " +
                 "material or the resolution changes).")]
        [Range(0, 6)]
        [SerializeField] int lensSkyFacesPerFrame = 1;

        [Tooltip("A WHITE hole's core (Docs/BLACK_HOLE.md §11): how bright the white at its centre is, in HDR " +
                 "units (the project has no tonemapper, so anything above 1 clips to pure white). The glow " +
                 "falls off toward the core's rim — the photon-capture radius, ~2.6 r_s — where the emitted " +
                 "light shows through.")]
        [Range(0f, 16f)]
        [SerializeField] float whiteCoreBrightness = 4f;

        [Tooltip("How much of the EMITTED light shows in a white hole's core: the sky that fell into its " +
                 "paired black hole, coming out the far side (the backward-traced ray continues through the " +
                 "tunnel into the sky). 0 = a pure white core, 1 = the sky at full strength under the glow.")]
        [Range(0f, 2f)]
        [SerializeField] float whiteCoreSkyMix = 0.8f;

        [Header("Pair (a black hole and a white hole born together — Docs/BLACK_HOLE.md §11)")]
        [Tooltip("Each hole's distance from the pair's midpoint at birth, in horizon radii — the black hole " +
                 "to one side, the white hole to the other, on the camera's (the vessel's) own horizontal. " +
                 "Nearer than ~3 the vessel between them sits inside both shadows.")]
        [Range(1.5f, 30f)]
        [SerializeField] float pairHalfGapHorizons = 4f;

        [Tooltip("How fast the two holes CLOSE on each other once let go, u/s (the Stoat's " +
                 "when its trigger is released). They fall together, accelerating to this over " +
                 "pairCloseRampSeconds, and annihilate where their horizons touch.")]
        [Range(0f, 200f)]
        [SerializeField] float pairDriftSpeed = 20f;

        [Tooltip("Fallback seconds after which a let-go pair with nothing closing it (speed 0) annihilates anyway.")]
        [Range(0.5f, 30f)]
        [SerializeField] float pairLifetime = 5f;

        [Tooltip("Seconds a let-go pair takes to reach its closing speed — the two start from rest and fall " +
                 "together, as two attracting masses would.")]
        [Range(0f, 5f)]
        [SerializeField] float pairCloseRampSeconds = 0.6f;

        [Header("Pair style (Docs/BLACK_HOLE.md §13)")]
        [Tooltip("Which wormhole pair every pair spawn lays — the Stoat's sling. " +
                 "OFF: the DRIFT pair (this branch, §11): a black attractor with a shadow and a " +
                 "white-hot repulsor, the physical pull, drifting apart and annihilating over pairLifetime (the " +
                 "Stoat's own sling life on StoatSlingConfig). ON: the CRYSTAL wormhole (charming-cerf, §12): two " +
                 "smooth wells with the graded lens and the felt pull, seamless mouths that carry the pilot through, " +
                 "formed and annihilated along CrystalWormhole.Curve with the Crystal Pair numbers below.")]
        [SerializeField] bool crystalPairs = false;

        [Header("Crystal pair (crystalPairs on — Docs/CRYSTAL_WORMHOLE.md)")]
        [Tooltip("Seconds a crystal pair takes to FORM out of nothing at its midpoint, spiralling apart. The " +
                 "crystal cell ships 6 s; a sling has to be live almost at once.")]
        [Range(0.05f, 10f)]
        [SerializeField] float crystalFormSeconds = 0.6f;

        [Tooltip("Seconds it STANDS at full strength between forming and annihilating.")]
        [Range(0.01f, 30f)]
        [SerializeField] float crystalStandSeconds = 0.05f;

        [Tooltip("Seconds it takes to ANNIHILATE: the poles spiral together, beat against each other and cancel. " +
                 "The crystal cell ships 9 s. Form + stand + annihilate = 4 s matches the drift pair's sling life.")]
        [Range(0.1f, 15f)]
        [SerializeField] float crystalAnnihilateSeconds = 3.35f;

        [Tooltip("Seconds a vessel's standing crystal pair takes to annihilate when its next sling replaces it — " +
                 "short, so a chained sling is not waiting on the last one's spiral.")]
        [Range(0.05f, 5f)]
        [SerializeField] float crystalReplaceSeconds = 0.4f;

        [Tooltip("Graded lens strength A (< 1, so the image never folds): the attractor magnifies what is behind " +
                 "it by up to 1/(1−A), the repulsor shrinks it by 1/(1+A). The lens is one throat wide.")]
        [Range(0f, 0.9f)]
        [SerializeField] float crystalLensStrength = 0.6f;

        [Tooltip("Felt pull on vessels, k: Sign · k · cruise² · R_throat · r / (r² + R_throat²)^1.5, in the hull's " +
                 "own cruise speed (Docs/CRYSTAL_WORMHOLE.md §3). The throat is the size dial a drift pair's horizon " +
                 "is (horizonPerStrength × strength).")]
        [Min(0f)]
        [SerializeField] float crystalFeltStrength = 4f;

        [Tooltip("The felt pull's ceiling, × the hull's cruise speed.")]
        [Min(0f)]
        [SerializeField] float crystalFeltCap = 1.3f;

        [Tooltip("How far the felt pull reaches, in throat radii.")]
        [Min(1f)]
        [SerializeField] float crystalFeltReach = 12f;

        [Tooltip("Turns the poles spiral through on the way in (CrystalWormhole.Curve).")]
        [Range(0f, 10f)]
        [SerializeField] float crystalSpiralTurns = 2.5f;

        [Tooltip("Amplitude beats on the way in, quickening.")]
        [Range(0f, 20f)]
        [SerializeField] float crystalBeatCycles = 7f;

        [Tooltip("How deep each beat swings the poles' amplitudes against each other (anti-phase).")]
        [Range(0f, 1f)]
        [SerializeField] float crystalBeatDepth = 0.5f;

        [Tooltip("The seamless mouth's material (WormholeSeamless.mat). Without it the pair warps space but " +
                 "carries no one.")]
        [SerializeField] Material crystalMouthMaterial;

        [Tooltip("Within this distance a mouth renders its exact view (else a panorama), world units.")]
        [Min(0f)]
        [SerializeField] float crystalMouthExactRange = 2500f;

        [Tooltip("The band over which the exact view fades to the panorama, world units.")]
        [Min(1f)]
        [SerializeField] float crystalMouthExactFadeBand = 600f;

        [Tooltip("Render scale of a mouth's exact view.")]
        [Range(0.1f, 1f)]
        [SerializeField] float crystalMouthExactRenderScale = 0.75f;

        [Tooltip("Face size of a mouth's panorama, pixels.")]
        [Range(32, 1024)]
        [SerializeField] int crystalMouthPanoramaFaceSize = 256;


        public bool LensEnabled => lensEnabled;
        public float LensRadiusMultiplier => Mathf.Clamp(lensRadiusMultiplier, 6f, 120f);
        public float LensFadeStart => Mathf.Clamp(lensFadeStart, 0.1f, 0.95f);
        public int LensSteps => Mathf.Clamp(lensSteps, 16, 192);
        public int LensSkyResolution => Mathf.Clamp(lensSkyResolution, 128, 2048);
        public int LensSkyFacesPerFrame => Mathf.Clamp(lensSkyFacesPerFrame, 0, 6);
        public float WhiteCoreBrightness => Mathf.Clamp(whiteCoreBrightness, 0f, 16f);
        public float WhiteCoreSkyMix => Mathf.Clamp(whiteCoreSkyMix, 0f, 2f);
        public float PairHalfGapHorizons => Mathf.Clamp(pairHalfGapHorizons, 1.5f, 30f);
        public float PairDriftSpeed => Mathf.Clamp(pairDriftSpeed, 0f, 200f);
        public float PairLifetime => Mathf.Clamp(pairLifetime, 0.5f, 30f);
        public float PairCloseRampSeconds => Mathf.Clamp(pairCloseRampSeconds, 0f, 5f);

        /// <summary>The pair style (§13): false = the drift pair, true = the crystal wormhole. Settable, so a
        /// test can flip it.</summary>
        public bool CrystalPairs { get => crystalPairs; set => crystalPairs = value; }
        public float CrystalFormSeconds => Mathf.Clamp(crystalFormSeconds, 0.05f, 10f);
        public float CrystalStandSeconds => Mathf.Clamp(crystalStandSeconds, 0.01f, 30f);
        public float CrystalAnnihilateSeconds => Mathf.Clamp(crystalAnnihilateSeconds, 0.1f, 15f);
        public float CrystalReplaceSeconds => Mathf.Clamp(crystalReplaceSeconds, 0.05f, 5f);
        public float CrystalLensStrength => Mathf.Clamp(crystalLensStrength, 0f, 0.9f);
        public float CrystalFeltStrength => Mathf.Max(0f, crystalFeltStrength);
        public float CrystalFeltCap => Mathf.Max(0f, crystalFeltCap);
        public float CrystalFeltReach => Mathf.Max(1f, crystalFeltReach);
        public float CrystalSpiralTurns => Mathf.Clamp(crystalSpiralTurns, 0f, 10f);
        public float CrystalBeatCycles => Mathf.Clamp(crystalBeatCycles, 0f, 20f);
        public float CrystalBeatDepth => Mathf.Clamp01(crystalBeatDepth);
        public Material CrystalMouthMaterial => crystalMouthMaterial;
        public float CrystalMouthExactRange => Mathf.Max(0f, crystalMouthExactRange);
        public float CrystalMouthExactFadeBand => Mathf.Max(1f, crystalMouthExactFadeBand);
        public float CrystalMouthExactRenderScale => Mathf.Clamp(crystalMouthExactRenderScale, 0.1f, 1f);
        public int CrystalMouthPanoramaFaceSize => Mathf.Clamp(crystalMouthPanoramaFaceSize, 32, 1024);

        public float GmPerStrength => Mathf.Max(0f, gmPerStrength);
        public float HorizonPerStrength => Mathf.Max(0.01f, horizonPerStrength);
        public float MinHorizonRadius => Mathf.Max(0.01f, minHorizonRadius);
        public float InfluenceAccelerationFloor => Mathf.Max(0.0001f, influenceAccelerationFloor);
        public float MaxInfluenceRadius => Mathf.Max(1f, maxInfluenceRadius);
        public float Spin => Mathf.Clamp(spin, 0f, 0.998f);
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
        public float TidalResponseSeconds => Mathf.Clamp(tidalResponseSeconds, 0f, 3f);
        public float MaxTidalStretch => Mathf.Clamp(maxTidalStretch, 1.5f, 30f);
        public float VesselTideScale => Mathf.Clamp01(vesselTideScale);
        public float WarpReachMultiplier => Mathf.Max(1.01f, warpReachMultiplier);
        public float WarpEaseSeconds => Mathf.Max(0f, warpEaseSeconds);

        /// <summary>Gravitational parameter of a hole of the given strength.</summary>
        public float GM(float strength) => Mathf.Max(0f, strength) * GmPerStrength;

        /// <summary>Event-horizon radius of a hole of the given strength (its size derived from it).</summary>
        public float HorizonRadius(float strength) =>
            Mathf.Max(MinHorizonRadius, Mathf.Max(0f, strength) * HorizonPerStrength);

        /// <summary>
        /// Event-horizon radius of a hole with an explicit <paramref name="size"/> (world units);
        /// a size of 0 or less means "derived from strength".
        /// </summary>
        public float HorizonRadius(float strength, float size) =>
            size > 0f ? Mathf.Max(MinHorizonRadius, size) : HorizonRadius(strength);

        /// <summary>
        /// Radius at which the pull falls to <see cref="InfluenceAccelerationFloor"/>, capped at
        /// <see cref="MaxInfluenceRadius"/> and never inside the horizon.
        /// </summary>
        public float InfluenceRadius(float strength) => InfluenceRadius(strength, HorizonRadius(strength));

        /// <summary>
        /// <see cref="InfluenceRadius(float)"/> for a hole whose horizon is
        /// <paramref name="horizonRadius"/> (its size may not be the one its strength implies).
        /// Never inside 1.5 horizons, even when that exceeds the cap — a hole always pulls the shell
        /// around its own horizon.
        /// </summary>
        public float InfluenceRadius(float strength, float horizonRadius)
        {
            float rs = Mathf.Max(MinHorizonRadius, horizonRadius);
            float gm = GM(strength);
            // Paczynski-Wiita: a = GM / (d - rs)^2  =>  d = rs + sqrt(GM / a_floor).
            float d = rs + Mathf.Sqrt(gm / InfluenceAccelerationFloor);
            return Mathf.Max(rs * 1.5f, Mathf.Min(d, MaxInfluenceRadius));
        }

        /// <summary>World units the warp reaches beyond a hole's horizon.</summary>
        public float WarpReach(float strength) => WarpReachForHorizon(HorizonRadius(strength));

        /// <summary>World units the warp reaches beyond a horizon of <paramref name="horizonRadius"/>.</summary>
        public float WarpReachForHorizon(float horizonRadius) =>
            Mathf.Max(MinHorizonRadius, horizonRadius) * (WarpReachMultiplier - 1f);

        /// <summary>
        /// The tidal log-stretch ε at distance <paramref name="d"/> from a hole of gravitational
        /// parameter <paramref name="gm"/>: <c>GM·τ²/d³</c> (radial ×e^ε, transverse ×e^(−ε/2)), before
        /// the shader's fade and ceiling — what the warp publishes as <c>GM·τ²</c> per hole.
        /// </summary>
        public float TidalLogStretch(float gm, float d) =>
            Mathf.Max(0f, gm) * TidalResponseSeconds * TidalResponseSeconds / Mathf.Max(d * d * d, 1e-6f);

        /// <summary>
        /// The shape the shader and the integrator can actually run: a positive horizon, a reach
        /// beyond it, a stretch ceiling above 1. An insane asset degrades to "off" rather than to
        /// a divide by zero.
        /// </summary>
        public bool IsSane =>
            HorizonPerStrength > 0f && MinHorizonRadius > 0f &&
            WarpReachMultiplier > 1f && MaxTidalStretch > 1f && TidalResponseSeconds >= 0f &&
            InfluenceAccelerationFloor > 0f;
    }
}
