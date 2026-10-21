using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every number of the Goby &amp; Pistol duo that the game runs, CLONED from the Vessel Studio artifact's Goby &amp;
    /// Pistol lab (<c>Docs/Studios/VesselStudio/goby.html</c>; the design is
    /// <c>.claude/skills/duo-vessel/references/designs/goby-and-pistol.md</c>). The artifact is the source of truth
    /// (/artifact-to-unity): each field is named by the artifact's own key, its tooltip is the artifact's label, its
    /// default is the artifact's SHIPPED value, and <c>Tools/Build/studio_to_unity.py --vessel goby</c> writes the asset
    /// (never edit a mapped number by hand; <c>--check</c> reports it as drift). Units are the lab's: world units and
    /// seconds; the Pistol's numbers are in its unrolled plane (x along the Goby's path, h up).
    ///
    /// <para>The Goby's cruise is <c>Goby.prefab</c>'s own DefaultThrottleScaler (the artifact's gCruise, cloned by the same
    /// script); its turn rates are the Squirrel's (two thumbs). The closeness speed and the AI's gap easing live here and
    /// scale that cruise (<c>GobyMath</c>). Read by <c>GobyPistolDuo</c> (on <c>Goby.prefab</c>, or added to any hull from
    /// the Play-mode menu).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "GobyPistolConfig", menuName = "ScriptableObjects/Vessel Actions/Goby Pistol Config")]
    public sealed class GobyPistolConfigSO : ScriptableObject
    {
        [Header("The Pistol's world")]
        [Tooltip("Unroll: how the Goby's path is unrolled into the Pistol's 2D world. The lab's default is World up (exact unroll).")]
        [SerializeField] PistolUnroll unroll = PistolUnroll.WorldUp;

        [Header("Goby: the primer (the artifact's Goby section)")]
        [Tooltip("Primer charges. \"Generous charges\".")]
        [SerializeField] float gPrimerCharges = 6f;

        [Tooltip("Primer s / charge. \"A relatively short cooldown\".")]
        [SerializeField] float gPrimerCd = 1.5f;

        [Tooltip("gPrimerRange")]
        [SerializeField] float gPrimerRange = 380f;

        [Tooltip("Oil splash u. Everything this close to the hit is primed.")]
        [SerializeField] float gPrimeSplash = 34f;

        [Tooltip("Prime lasts s. Long enough for the Pistol to try one recovery: time it here.")]
        [SerializeField] float gPrimeLife = 12f;

        [Header("Goby: the flares (the artifact's Goby section)")]
        [Tooltip("Flare charges")]
        [SerializeField] float gFlareCharges = 2f;

        [Tooltip("Flare s / charge. Longer than the primer's.")]
        [SerializeField] float gFlareCd = 10f;

        [Tooltip("gFlareRange")]
        [SerializeField] float gFlareRange = 420f;

        [Tooltip("Flares per volley. One flare per hot block in range, up to this.")]
        [SerializeField] float gFlareMax = 10f;

        [Tooltip("gFlareSpeed")]
        [SerializeField] float gFlareSpeed = 240f;

        [Header("Goby: speed with the Pistol behind it (the artifact's Goby section)")]
        [Tooltip("Closeness ×. The Goby's speed with the Pistol right on its tail.")]
        [SerializeField] float gCloseMax = 2.4f;

        [Tooltip("Closeness range u. Beyond this gap the Pistol adds no speed.")]
        [SerializeField] float gCloseRange = 320f;

        [Header("Pistol: the ride (the artifact's Pistol section)")]
        [Tooltip("Pull u/s². The magnet that holds the Pistol to the ground line.")]
        [SerializeField] float pGravity = 140f;

        [Tooltip("Dive ×. The pull while diving (Tiny Wings: heavy on the way down).")]
        [SerializeField] float pDiveMul = 3.2f;

        [Tooltip("Min speed")]
        [SerializeField] float pMinSpeed = 35f;

        [Tooltip("pDrag. Per second")]
        [SerializeField] float pDrag = 0.04f;

        [Tooltip("Smooth landing °. Land within this angle of the slope and keep your speed.")]
        [SerializeField] float pGoodLand = 28f;

        [Header("Pistol: the screen (the artifact's Pistol section)")]
        [Tooltip("Vision band u. How far either side of the trail the Pistol sees; its body reaches 4 u past it.")]
        [SerializeField] float pVision = 45f;

        [Tooltip("pBodyR. The cylinder's radius (its length reaches 4 u past the vision band)")]
        [SerializeField] float pBodyR = 5f;

        [Tooltip("Screen width u. How much ribbon the Pistol's screen shows.")]
        [SerializeField] float pViewWidth = 460f;

        [Tooltip("Parallax depth. Layers this deep draw at half size.")]
        [SerializeField] float pParallax = 160f;

        [Header("Pistol: the views (the crew vessels' one layout and look: the Wrasse's KiteNavigatorConfig names and defaults)")]
        [Tooltip("The small view's side, as a fraction of the screen height: the Pistol's view while you fly the Goby, the Goby's while you fly the Pistol.")]
        [SerializeField, Range(0.15f, 0.6f)] float pipSize = 0.32f;

        [Tooltip("The small view's gap from the screen's edges, as a fraction of the screen height.")]
        [SerializeField, Range(0f, 0.1f)] float viewMargin = 0.015f;

        [Tooltip("For the Pistol's view only, the prisms' depth look measures distance from the Pistol's plane: a prism this far (u) " +
                 "out of it reads as far as the Goby's view's most distant mass; one on the plane reads near.")]
        [SerializeField, Min(1f)] float depthFullSpreadAt = 60f;

        [Header("Each seat's buttons (game-side, not in the artifact: the Orca's gunner buttons are the pattern)")]
        [Tooltip("The Goby's Primer button: on a phone round the right thumb, on a pad X, on a keyboard Q (the hull binds " +
                 "Button3Action to CrewButtonPrimer).")]
        [SerializeField] Sprite primerButtonIcon;
        [Tooltip("Player-facing caption under the Primer button. Empty shows none.")]
        [SerializeField] string primerButtonLabel = "";
        [Tooltip("The Goby's Flares button: on a phone round the right thumb, on a pad Y, on a keyboard F (Button4Action " +
                 "to CrewButtonFlares).")]
        [SerializeField] Sprite flaresButtonIcon;
        [Tooltip("Player-facing caption under the Flares button. Empty shows none.")]
        [SerializeField] string flaresButtonLabel = "";
        [Tooltip("The Pistol's Ghost button (held), round the right thumb on a phone while you fly the Pistol.")]
        [SerializeField] Sprite ghostButtonIcon;
        [Tooltip("Player-facing caption under the Ghost button. Empty shows none.")]
        [SerializeField] string ghostButtonLabel = "";
        [Tooltip("The Pistol's Snap button (the vortex ring), round the right thumb on a phone while you fly the Pistol.")]
        [SerializeField] Sprite snapButtonIcon;
        [Tooltip("Player-facing caption under the Snap button. Empty shows none.")]
        [SerializeField] string snapButtonLabel = "";
        [Tooltip("A button's charge count. {count} is the number; empty shows none.")]
        [SerializeField] string buttonCountTemplate = "{count}";

        [Header("Pistol: launch and ghost (the artifact's Pistol section)")]
        [Tooltip("Launch range")]
        [SerializeField] float pLaunchRange = 240f;

        [Tooltip("pLaunchSpeed")]
        [SerializeField] float pLaunchSpeed = 280f;

        [Tooltip("pLaunchCharges")]
        [SerializeField] float pLaunchCharges = 1f;

        [Tooltip("Launch s / charge")]
        [SerializeField] float pLaunchCd = 9f;

        [Tooltip("Ghost s")]
        [SerializeField] float pGhostTime = 2.5f;

        [Tooltip("pGhostCd")]
        [SerializeField] float pGhostCd = 8f;

        [Header("Pistol: primes going up (the artifact's Pistol section)")]
        [Tooltip("Chain u. Primed blocks this close go up together.")]
        [SerializeField] float pChain = 40f;

        [Tooltip("Heat ring u. Unprimed blocks this close to a detonation turn hot.")]
        [SerializeField] float pHotRing = 80f;

        [Tooltip("pHotLife")]
        [SerializeField] float pHotLife = 10f;

        [Header("Pistol: the Snap, a vortex ring (the artifact's Pistol section)")]
        [Tooltip("Vortex radius. The Snap: a vortex ring. Its radius.")]
        [SerializeField] float pVortexR = 22f;

        [Tooltip("Vortex speed")]
        [SerializeField] float pVortexSpeed = 120f;

        [Tooltip("Vortex life s")]
        [SerializeField] float pVortexLife = 2.6f;

        [Tooltip("Vortex pull u. Prisms this close to the ring's core are pulled into its toroidal flow.")]
        [SerializeField] float pVortexCapture = 24f;

        [Tooltip("Vortex spin. Radians per second round the core.")]
        [SerializeField] float pVortexSpin = 7f;

        [Tooltip("Vortex carries. Prisms one ring can carry.")]
        [SerializeField] float pVortexMax = 30f;

        [Tooltip("Vortex cooldown")]
        [SerializeField] float pVortexCd = 4f;

        [Tooltip("Vortex collapses. 1: the ring collapses and breaks what it carries. 0: it lets them go where they are.")]
        [SerializeField] float pVortexCollapse = 1f;

        [Header("Trail and hills (the artifact's Trail section)")]
        [Tooltip("tSampleGap. The ribbon is sampled this often along the path")]
        [SerializeField] float tSampleGap = 4f;

        [Tooltip("Block gap u")]
        [SerializeField] float tBlockGap = 16f;

        [Tooltip("Hinge every N. Every Nth block is a hinge, alternately up and down.")]
        [SerializeField] float tHillEvery = 4f;

        [Tooltip("Hill height u. How far a hinge lifts or drops the ground. 0: the path alone.")]
        [SerializeField] float tHillAmp = 26f;

        [Header("AI (the artifact's AI section)")]
        [Tooltip("aiHighDive. The Pistol AI dives when this far above the ground")]
        [SerializeField] float aiHighDive = 120f;

        [Tooltip("Goby waits beyond u. The Goby AI eases off when the Pistol is further behind than this.")]
        [SerializeField] float aiMaxGap = 260f;

        public PistolUnroll Unroll => unroll;
        public float PrimerCharges => gPrimerCharges;
        public float PrimerCd => gPrimerCd;
        public float PrimerRange => gPrimerRange;
        public float PrimeSplash => gPrimeSplash;
        public float PrimeLife => gPrimeLife;
        public float FlareCharges => gFlareCharges;
        public float FlareCd => gFlareCd;
        public float FlareRange => gFlareRange;
        public float FlareMax => gFlareMax;
        public float FlareSpeed => gFlareSpeed;
        public float CloseMax => gCloseMax;
        public float CloseRange => gCloseRange;
        public float AiMaxGap => aiMaxGap;
        public float Vision => pVision;
        public float BodyR => pBodyR;
        public float ViewWidth => pViewWidth;
        public float Parallax => pParallax;
        public float PipSize => pipSize;
        public float ViewMargin => viewMargin;
        public Sprite PrimerButtonIcon => primerButtonIcon;
        public string PrimerButtonLabel => primerButtonLabel;
        public Sprite FlaresButtonIcon => flaresButtonIcon;
        public string FlaresButtonLabel => flaresButtonLabel;
        public Sprite GhostButtonIcon => ghostButtonIcon;
        public string GhostButtonLabel => ghostButtonLabel;
        public Sprite SnapButtonIcon => snapButtonIcon;
        public string SnapButtonLabel => snapButtonLabel;
        public string ButtonCountTemplate => buttonCountTemplate;
        public float DepthFullSpreadAt => depthFullSpreadAt;
        public float Chain => pChain;
        public float HotRing => pHotRing;
        public float HotLife => pHotLife;

        public PistolRibbon.LaySettings Lay => new() { SampleGap = tSampleGap, BlockGap = tBlockGap, HillEvery = tHillEvery, HillAmp = tHillAmp };

        public PistolBodyMath.Settings Body => new()
        {
            pGravity = pGravity, pDiveMul = pDiveMul, pMinSpeed = pMinSpeed, pDrag = pDrag, pGoodLand = pGoodLand,
            pLaunchRange = pLaunchRange, pLaunchSpeed = pLaunchSpeed, pLaunchCharges = pLaunchCharges, pLaunchCd = pLaunchCd,
            pGhostTime = pGhostTime, pGhostCd = pGhostCd, pVortexCd = pVortexCd,
        };

        public VortexRing.Settings Vortex => new()
        {
            pVortexR = pVortexR, pVortexSpeed = pVortexSpeed, pVortexLife = pVortexLife, pVortexCapture = pVortexCapture,
            pVortexSpin = pVortexSpin, pVortexMax = pVortexMax, pVortexCollapse = pVortexCollapse,
        };

        public PistolAI.Settings AI => new()
        {
            pLaunchRange = pLaunchRange, pVision = pVision, pVortexR = pVortexR, pVortexCapture = pVortexCapture, aiHighDive = aiHighDive,
        };
    }
}
