using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every number of the Goby &amp; Pistol crew vessel (Garrett, 2026-10-22: "it should be a goby with its own kit that
    /// represents a goby and synergizes with the pistol and its kit"; built straight in Unity, the lab is behind it). Read
    /// by <c>GobyPistolDuo</c> on <c>Goby.prefab</c>. The design and its decisions:
    /// <c>.claude/skills/duo-vessel/references/designs/goby-and-pistol.md</c> §11 (2026-10-22).
    ///
    /// <para><b>The pair's four elements</b> (one meter, both seats):</para>
    /// <list type="bullet">
    /// <item><b>Charge</b>: petals a vortex ring takes from a rival hull it hits (<see cref="RingPetals"/>).</item>
    /// <item><b>Mass</b>: den prisms per rest (<see cref="DenMassScale"/>); level 5 adds danger and super-shielded prisms.</item>
    /// <item><b>Space</b>: how far either side of the Pistol's plane mass is pulled into it (<see cref="PullReach"/>); level
    /// 5 pulls shielded prisms too.</item>
    /// <item><b>Time</b>: the Pistol's jump cooldown (<see cref="JumpCooldown"/>); level 5 lets the Goby's darts pass through
    /// prisms.</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(fileName = "GobyPistolConfig", menuName = "ScriptableObjects/Vessel Actions/Goby Pistol Config")]
    public sealed class GobyPistolConfigSO : ScriptableObject
    {
        [Header("Goby: dart and perch (the Shrike's sling: distance / dartSeconds, docks on the first block it touches)")]
        [Tooltip("The farthest a full-charged dart goes, u.")]
        [SerializeField, Min(10f)] float dartRange = 600f;
        [Tooltip("How far a tap darts, u. Holding the dart button charges it up to dartRange over dartChargeSeconds.")]
        [SerializeField, Min(5f)] float dartMinRange = 300f;
        [Tooltip("Seconds of holding the dart button to reach full range.")]
        [SerializeField, Min(0.05f)] float dartChargeSeconds = 0.8f;
        [Tooltip("Seconds a dart takes, whatever its length (the Shrike's slingSeconds).")]
        [SerializeField, Min(0.1f)] float dartSeconds = 1f;
        [Tooltip("Seconds after a dart lands before the next can start.")]
        [SerializeField, Min(0f)] float dartCooldown = 1.2f;
        [Tooltip("The hull's radius against a block's box: a dart stops this far off the block it reaches (the Shrike's 3).")]
        [SerializeField, Min(0.5f)] float hullRadius = 3f;
        [Tooltip("Extra reach of the spatial query round the dart's line, u.")]
        [SerializeField, Min(0f)] float queryReach = 6f;
        [Tooltip("Blocks in the pad the Goby lays when it darts into open space and makes its own perch.")]
        [SerializeField, Range(1, 12)] int ownPerchBlocks = 5;
        [Tooltip("A perch pad block's edge, u.")]
        [SerializeField, Min(0.5f)] float perchBlockSize = 4f;

        [Header("Goby: lock (vessels first, then fauna: the Shrike's lock order)")]
        [Tooltip("How far the Goby can lock, u.")]
        [SerializeField, Min(10f)] float lockRange = 1400f;
        [Tooltip("The lock cone's half-angle, degrees, round the Goby's nose.")]
        [SerializeField, Range(1f, 45f)] float lockCone = 12f;
        [Tooltip("A held lock is kept to this many times the cone and the range.")]
        [SerializeField, Min(1f)] float lockKeepScale = 1.6f;

        [Header("The Pistol's ground (the dart's own trail on a flat plane)")]
        [Tooltip("Degrees the travel from perch to perch runs downhill on the Pistol's screen, either way it rides. Past 33 " +
                 "the hills could fold the ground back on itself (PistolPlane.MaxHillSlope).")]
        [SerializeField, Range(0f, 33f)] float slopeDegrees = 22f;
        [Tooltip("The rolling hills, u either side of the dart's line along the Goby's up: the long hills a dive and a " +
                 "release are played on. A whole number of half-waves per dart, so zero at both perches. The Pistol's world is " +
                 "shrimp-sized (Garrett, 2026-10-22: \"zoom in, make the shrimp smaller and make more wiggles\").")]
        [SerializeField, Min(0f)] float rollAmplitude = 3.5f;
        [Tooltip("The rolling hills' wavelength, u.")]
        [SerializeField, Min(5f)] float rollWavelength = 60f;
        [Tooltip("The humps on top of the rolls, u high. A flight off one clears a few of them.")]
        [SerializeField, FormerlySerializedAs("hillAmplitude"), Min(0f)] float fineAmplitude = 1.6f;
        [Tooltip("The humps' wavelength, u: a few humps are a small share of a 300-600 u journey.")]
        [SerializeField, FormerlySerializedAs("hillWavelength"), Min(5f)] float fineWavelength = 10f;
        [Tooltip("A trail block every this many u along the dart.")]
        [SerializeField, Min(2f)] float trailBlockGap = 3f;
        [Tooltip("A trail block's size (x across, y thick, z along the dart).")]
        [SerializeField] Vector3 trailBlockScale = new(1.4f, 0.4f, 2.8f);

        [Header("Pistol: the ride (Tiny Wings at shrimp scale: a 300-600 u dart is a 15-30 s journey for a rider who dives " +
                "on the downslopes, and a flight off a hump clears a few humps)")]
        [SerializeField, Min(1f)] float pGravity = 30f;
        [SerializeField, Min(1f)] float pDiveMul = 2.2f;
        [SerializeField, Min(0f)] float pMinSpeed = 4f;
        [SerializeField, Min(0f)] float pDrag = 0.45f;
        [Tooltip("Degrees between the velocity and the slope that still count as a smooth landing.")]
        [SerializeField, Min(0f)] float pGoodLand = 28f;
        [Tooltip("The Pistol's body radius in its plane, u.")]
        [SerializeField, Min(0.5f)] float pBodyR = 0.5f;
        [Tooltip("How far either side of its plane the Pistol's body reaches, u (pulled-in mass lands inside it).")]
        [SerializeField, Min(0.5f)] float pBodyHalfDepth = 10f;
        [Tooltip("The jump's farthest reach in the plane, u.")]
        [SerializeField, Min(10f)] float pLaunchRange = 35f;
        [Tooltip("Off: the Pistol has no aimed jump; it flies only off the hills, by letting go of the dive on an upslope " +
                 "(Garrett, 2026-10-22: \"launching intentionally was wierd\"). On: drag (RT + right stick, F) aims a jump on " +
                 "Time's cooldown, up to pLaunchRange.")]
        [SerializeField] bool aimedJump;
        [SerializeField, Min(10f)] float pLaunchSpeed = 30f;
        [SerializeField, Min(1f)] float pLaunchCharges = 1f;
        [Tooltip("Time: the jump's cooldown, s (9 at rest -> 3 at level 10).")]
        [SerializeField] ElementalFloat jumpCooldown = ElementalFloat.Multiplier(9f, 3f, Element.Time, 1f);
        [SerializeField, Min(0.1f)] float pGhostTime = 2.5f;
        [SerializeField, Min(0.1f)] float pGhostCd = 8f;
        [Tooltip("A slingshot pull this long (a share of the screen's short side) is a full jump.")]
        [SerializeField, Range(0.05f, 0.6f)] float pSlingPull = 0.22f;

        [Header("Pistol: perches and dens")]
        [Tooltip("The Pistol arrives at a perch within this many u of it.")]
        [SerializeField, Min(1f)] float arriveDistance = 2f;
        [Tooltip("Seconds the Pistol rests at a perch, building its den.")]
        [SerializeField, Min(0.1f)] float restSeconds = 1.2f;
        [Tooltip("One den prism per this many u travelled since the last den (before Mass).")]
        [SerializeField, Min(1f)] float denUnitsPerPrism = 40f;
        [Tooltip("Mass: den prisms are scaled by this (x1 at rest -> x2.5 at level 10).")]
        [SerializeField] ElementalFloat denMassScale = ElementalFloat.Multiplier(1f, 2.5f, Element.Mass, 0.25f);
        [Tooltip("The most prisms one den holds.")]
        [SerializeField, Range(4, 200)] int denMax = 80;
        [Tooltip("Share of a den that is shielded.")]
        [SerializeField, Range(0f, 1f)] float denShieldedShare = 0.4f;
        [Tooltip("With the Mass upgrade: share of a den that is danger prisms.")]
        [SerializeField, Range(0f, 1f)] float denDangerShare = 0.15f;
        [Tooltip("With the Mass upgrade: share of a den that is super-shielded.")]
        [SerializeField, Range(0f, 1f)] float denSuperShare = 0.12f;
        [Tooltip("A den block's edge, u.")]
        [SerializeField, Min(0.5f)] float denBlockSize = 4f;
        [Tooltip("The den's first shell radius round the perch, u.")]
        [SerializeField, Min(1f)] float denFirstRadius = 10f;
        [Tooltip("Den blocks keep their bounding diameter times this apart (no two ever clip).")]
        [SerializeField, Min(1f)] float denClearance = 1.1f;

        [Header("Pistol: the undertow (mass pulled into the plane)")]
        [Tooltip("Space: how far either side of the plane mass is pulled in, u (40 at rest -> 200 at level 10). Danger, " +
                 "super-shielded and the pair's own prisms never move; shielded ones only with the Space upgrade.")]
        [SerializeField] ElementalFloat pullReach = ElementalFloat.Multiplier(40f, 200f, Element.Space, 10f);
        [Tooltip("How fast pulled mass moves toward the plane, u/s.")]
        [SerializeField, Min(1f)] float pullSpeed = 60f;
        [Tooltip("Mass this far along the plane either side of the Pistol is pulled, u.")]
        [SerializeField, Min(10f)] float pullWindow = 320f;
        [Tooltip("At most this many prisms move each frame.")]
        [SerializeField, Range(8, 512)] int pullMaxPerFrame = 96;

        [Header("Pistol: the Snap (a vortex ring)")]
        [SerializeField, Min(1f)] float pVortexR = 22f;
        [SerializeField, Min(1f)] float pVortexSpeed = 120f;
        [SerializeField, Min(0.1f)] float pVortexLife = 2.6f;
        [SerializeField, Min(0f)] float pVortexCapture = 24f;
        [SerializeField, Min(0f)] float pVortexSpin = 7f;
        [SerializeField, Min(1f)] float pVortexMax = 30f;
        [SerializeField, Min(0.1f)] float pVortexCd = 4f;
        [Tooltip("1: the ring collapses and breaks what it carries. 0: it lets them go.")]
        [SerializeField, Range(0f, 1f)] float pVortexCollapse = 1f;
        [Tooltip("A ring fired at the Goby's locked rival turns toward it at this many degrees a second.")]
        [SerializeField, Min(0f)] float homingTurnRate = 240f;
        [Tooltip("Charge: petals the ring takes from EACH element of a rival hull it hits (1 at rest -> 3 at level 10).")]
        [SerializeField] ElementalFloat ringPetals = ElementalFloat.Multiplier(1f, 3f, Element.Charge, 1f);
        [Tooltip("A ring hits a hull within its radius plus this, u.")]
        [SerializeField, Min(0f)] float ringHitPad = 4f;
        [Tooltip("The scored hit a ring lands (the PvP rule: a hit takes petals and nothing else).")]
        [SerializeField] CombatHitClass ringHitClass = CombatHitClass.MissileDirect;
        [Tooltip("Seconds before the same ring can score on the same hull again (the latch window).")]
        [SerializeField, Min(0f)] float hitCooldown = 0.25f;
        [Tooltip("Raised for every scored ring hit (the fleet's combat scoring).")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;
        [Tooltip("The match's players: the Goby locks rival vessels from it, and the rings hit them (the Orca's crew reads the same asset).")]
        [SerializeField] GameDataSO gameData;

        [Header("Pistol: the views (the crew vessels' one layout and look)")]
        [Tooltip("The small view's side, as a fraction of the screen height.")]
        [SerializeField, Range(0.15f, 0.6f)] float pipSize = 0.32f;
        [Tooltip("The small view's gap from the screen's edges, as a fraction of the screen height.")]
        [SerializeField, Range(0f, 0.1f)] float viewMargin = 0.015f;
        [Tooltip("How much of the plane the Pistol's screen shows across, u, at the plane.")]
        [SerializeField, Min(50f)] float viewWidth = 120f;
        [Tooltip("How far the Pistol's camera sits from its plane, u: close, so the world either side of the plane reads " +
                 "in depth (the lab's and the first Unity side view's 160). Its lens is off-axis: the camera stays beside the " +
                 "Pistol and the screen looks ahead, so the Pistol is never stretched at the edge of a wide lens.")]
        [SerializeField, Min(20f)] float viewDistance = 160f;
        [Tooltip("Where the Pistol sits across its screen, as a share of the width from the centre (minus: left of it, so " +
                 "more of the downhill ahead shows).")]
        [SerializeField, Range(-0.45f, 0.45f)] float viewPistolAt = -0.2f;
        [Tooltip("For the Pistol's view only, a prism this far out of its plane reads as far as the Goby's view's most distant mass.")]
        [SerializeField, Min(1f)] float depthFullSpreadAt = 60f;

        [Header("AI")]
        [Tooltip("The Goby AI darts this far, u, when it can choose.")]
        [SerializeField, Min(10f)] float aiDartLength = 450f;
        [Tooltip("The Goby AI waits this long for the Pistol to reach its perch before it darts on anyway, s.")]
        [SerializeField, Min(0f)] float aiWaitForPistol = 25f;
        [Tooltip("The Goby AI keeps within this distance of where it started, u.")]
        [SerializeField, Min(100f)] float aiHomeRadius = 1600f;
        [Tooltip("The Pistol AI heads back to an old den after waiting this long at the head perch, s.")]
        [SerializeField, Min(0f)] float aiBackAfter = 4f;
        [Tooltip("The Pistol AI dives when this far above its ground, u.")]
        [SerializeField, Min(0f)] float aiHighDive = 30f;

        [Header("Each seat's buttons on a phone (the Orca's gunner buttons are the pattern)")]
        [Tooltip("The Goby's Dart button (held to charge, let go to dart). A pad's RT, the keyboard's Q.")]
        [SerializeField] Sprite dartButtonIcon;
        [Tooltip("Player-facing caption under the Dart button. Empty shows none.")]
        [SerializeField] string dartButtonLabel = "";
        [Tooltip("The Goby's Lock button. A pad's LT, the keyboard's F.")]
        [SerializeField] Sprite lockButtonIcon;
        [Tooltip("Player-facing caption under the Lock button. Empty shows none.")]
        [SerializeField] string lockButtonLabel = "";
        [Tooltip("The Pistol's Ghost button (held).")]
        [SerializeField] Sprite ghostButtonIcon;
        [Tooltip("Player-facing caption under the Ghost button. Empty shows none.")]
        [SerializeField] string ghostButtonLabel = "";
        [Tooltip("The Pistol's Snap button (the vortex ring).")]
        [SerializeField] Sprite snapButtonIcon;
        [Tooltip("Player-facing caption under the Snap button. Empty shows none.")]
        [SerializeField] string snapButtonLabel = "";
        [Tooltip("The Pistol's Back button: at a perch, head back toward the older dens (press again to head on).")]
        [SerializeField] Sprite backButtonIcon;
        [Tooltip("Player-facing caption under the Back button. Empty shows none.")]
        [SerializeField] string backButtonLabel = "";

        public float DartRange => dartRange;
        public float DartMinRange => dartMinRange;
        public float DartChargeSeconds => dartChargeSeconds;
        public float DartSeconds => dartSeconds;
        public float DartCooldown => dartCooldown;
        public float HullRadius => hullRadius;
        public float QueryReach => queryReach;
        public int OwnPerchBlocks => ownPerchBlocks;
        public float PerchBlockSize => perchBlockSize;
        public float LockRange => lockRange;
        public float LockCone => lockCone;
        public float LockKeepScale => lockKeepScale;
        public float SlopeDegrees => slopeDegrees;
        public PistolPlane.Hills Hills => new(rollAmplitude, rollWavelength, fineAmplitude, fineWavelength);
        public float TrailBlockGap => trailBlockGap;
        public Vector3 TrailBlockScale => trailBlockScale;
        public float BodyR => pBodyR;
        public float BodyHalfDepth => pBodyHalfDepth;
        public float SlingPull => pSlingPull;
        public bool AimedJump => aimedJump;
        public ElementalFloat JumpCooldown => jumpCooldown;
        public float ArriveDistance => arriveDistance;
        public float RestSeconds => restSeconds;
        public float DenUnitsPerPrism => denUnitsPerPrism;
        public ElementalFloat DenMassScale => denMassScale;
        public int DenMax => denMax;
        public float DenShieldedShare => denShieldedShare;
        public float DenDangerShare => denDangerShare;
        public float DenSuperShare => denSuperShare;
        public float DenBlockSize => denBlockSize;
        public float DenFirstRadius => denFirstRadius;
        public float DenClearance => denClearance;
        public ElementalFloat PullReach => pullReach;
        public float PullSpeed => pullSpeed;
        public float PullWindow => pullWindow;
        public int PullMaxPerFrame => pullMaxPerFrame;
        public float HomingTurnRate => homingTurnRate;
        public ElementalFloat RingPetals => ringPetals;
        public float RingHitPad => ringHitPad;
        public CombatHitClass RingHitClass => ringHitClass;
        public float HitCooldown => hitCooldown;
        public ScriptableEventCombatHitStats OnCombatHitLanded => onCombatHitLanded;
        public GameDataSO GameData => gameData;
        public float PipSize => pipSize;
        public float ViewMargin => viewMargin;
        public float ViewWidth => viewWidth;
        public float ViewDistance => viewDistance;
        public float ViewPistolAt => viewPistolAt;
        public float DepthFullSpreadAt => depthFullSpreadAt;
        public float AiDartLength => aiDartLength;
        public float AiWaitForPistol => aiWaitForPistol;
        public float AiHomeRadius => aiHomeRadius;
        public float AiBackAfter => aiBackAfter;
        public Sprite DartButtonIcon => dartButtonIcon;
        public string DartButtonLabel => dartButtonLabel;
        public Sprite LockButtonIcon => lockButtonIcon;
        public string LockButtonLabel => lockButtonLabel;
        public Sprite GhostButtonIcon => ghostButtonIcon;
        public string GhostButtonLabel => ghostButtonLabel;
        public Sprite SnapButtonIcon => snapButtonIcon;
        public string SnapButtonLabel => snapButtonLabel;
        public Sprite BackButtonIcon => backButtonIcon;
        public string BackButtonLabel => backButtonLabel;

        /// <summary>The ride's numbers; <c>pLaunchCd</c> is filled by the caller from <see cref="JumpCooldown"/>.</summary>
        public PistolBodyMath.Settings Body => new()
        {
            pGravity = pGravity, pDiveMul = pDiveMul, pMinSpeed = pMinSpeed, pDrag = pDrag, pGoodLand = pGoodLand,
            pLaunchRange = pLaunchRange, pLaunchSpeed = pLaunchSpeed, pLaunchCharges = pLaunchCharges, pLaunchCd = jumpCooldown.Value,
            pGhostTime = pGhostTime, pGhostCd = pGhostCd, pVortexCd = pVortexCd,
        };

        public VortexRing.Settings Vortex => new()
        {
            pVortexR = pVortexR, pVortexSpeed = pVortexSpeed, pVortexLife = pVortexLife, pVortexCapture = pVortexCapture,
            pVortexSpin = pVortexSpin, pVortexMax = pVortexMax, pVortexCollapse = pVortexCollapse,
        };

        public PistolAI.Settings AI => new()
        {
            pLaunchRange = pLaunchRange, pVortexR = pVortexR, pVortexCapture = pVortexCapture, aiHighDive = aiHighDive, bodyHalfDepth = pBodyHalfDepth, aimedJump = aimedJump,
        };
    }
}
