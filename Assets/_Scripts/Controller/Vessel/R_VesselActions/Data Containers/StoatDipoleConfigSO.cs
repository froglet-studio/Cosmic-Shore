using CosmicShore.Data;
using CosmicShore.Gameplay;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every number of the Stoat's FIELD DIPOLE (Space) and PATHFINDER (Time)
    /// (<c>R_VesselActions/STOAT_DIPOLE.md</c>), ported from the Stoat Flight Studio's round-15 row
    /// (<c>Docs/Studios/StoatFlightStudio.html</c>, the <c>ft*</c> parameters). The defaults ARE that
    /// row; units are world units and seconds, the studio's own (its cruise is the Stoat's 60 u/s).
    ///
    /// <para><b>The dipole.</b> A squeeze lays one sink–source pair <see cref="AheadDistance"/> in front of
    /// the hull, the poles together, in the frame the hull had then. The triggers' DIFFERENCE pulls the
    /// poles apart sideways (the sink on the deeper trigger's side), their SUM lengthways (the sink always
    /// the nearer). Ease off and they come back together; let go of both and they meet and annihilate.
    /// The poles' strength and size are fixed by this file — and by Space (<see cref="PoleSize"/>) — so
    /// the only thing a trigger changes is the separation, the dipole moment.</para>
    ///
    /// <para><b>The pathfinder.</b> The hull's own flight run forward on this frame's inputs, drawn as
    /// dots from the nose. When the poles WARP that path at all (it bends at least
    /// <see cref="WarpDegrees"/>, or goes through the wormhole) it turns lime and the hull flies down it
    /// <see cref="Boost"/> × faster — a warp of the hull's own clock, so the path stays the path. Time
    /// scales the boost.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "StoatDipoleConfig", menuName = "ScriptableObjects/Vessel Actions/Stoat Dipole Config")]
    public sealed class StoatDipoleConfigSO : ScriptableObject
    {
        [Header("Squeeze")]
        [Tooltip("Squeeze curve exponent on each trigger: 1 = linear, >1 keeps a light squeeze close and makes the last bit of travel count.")]
        [SerializeField, Range(0.5f, 4f)] float holdExponent = 1.5f;

        [Tooltip("Keyboard / mouse / touch have no analog trigger: seconds of hold that count as a full squeeze.")]
        [SerializeField, Range(0.1f, 5f)] float holdRampSeconds = 1.5f;

        [Header("Placement (Space)")]
        [Tooltip("How far in front of the hull the pair's middle is laid, world units (the studio's ftAhead).")]
        [SerializeField, Min(0f)] float aheadDistance = 250f;

        [Tooltip("The sideways separation one full trigger asks for (the triggers' difference), world units (ftSepMax).")]
        [SerializeField, Min(0f)] float sidewaysMax = 200f;

        [Tooltip("The lengthways separation both full triggers ask for (the triggers' sum), world units (ftSepLong). The sink is always the nearer pole.")]
        [SerializeField, Min(0f)] float lengthwaysMax = 120f;

        [Tooltip("How quickly the poles follow the triggers, per second (ftSepFollow).")]
        [SerializeField, Range(0.5f, 30f)] float followRate = 6f;

        [Header("Poles (fixed — only their separation moves)")]
        [Tooltip("The sink's gravitational parameter GM, u³/s² (the studio's strength 6 × its 20000 per strength). " +
                 "Authored as GM, not as a BlackHoleConfig strength, so the shipped gmPerStrength cannot rescale it.")]
        [SerializeField, Min(0f)] float poleGM = 120000f;

        [Tooltip("Each pole's event-horizon radius, world units (ftHorizon).")]
        [SerializeField, Min(0.1f)] float poleHorizon = 3.5f;

        [Tooltip("Space is size: a multiplier on both poles' horizon AND their GM (a bigger hole is a heavier one). Read live on the owner, replicated to peers as the horizon.")]
        [SerializeField] ElementalFloat poleSize = ElementalFloat.Multiplier(1f, 2f, Element.Space, 1f);

        [Tooltip("The source's push as a share of the sink's pull (ftWhite). 1 = equal and opposite: a true dipole.")]
        [SerializeField, Range(0f, 2f)] float sourcePush = 1f;

        [Tooltip("The source's push is softened over this many of its own horizons (whPushSoften).")]
        [SerializeField, Range(0.1f, 10f)] float sourceSofteningHorizons = 2f;

        [Tooltip("The ceiling on the field's acceleration, u/s² (dpAccelCap).")]
        [SerializeField, Min(1f)] float accelerationCap = 3000f;

        [Tooltip("Seconds the sink takes to grow to full strength after a squeeze lays it (bornS).")]
        [SerializeField, Range(0.05f, 5f)] float sinkGrowSeconds = 0.9f;

        [Tooltip("Seconds the source takes to grow in (whInS); it starts at 55% of this, so a fresh pair pulls before it pushes.")]
        [SerializeField, Range(0.05f, 5f)] float sourceGrowSeconds = 1.1f;

        [Header("Flight in the field (the owner's)")]
        [Tooltip("The fastest the field may turn the nose, radians per second (dpTurnCap).")]
        [SerializeField, Range(0.5f, 40f)] float turnCap = 12f;

        [Tooltip("How firmly the engine pulls the field's gravity speed back toward cruise, per second (ftGrip). 0 = pure gravity.")]
        [SerializeField, Range(0f, 10f)] float grip = 0.5f;

        [Tooltip("The gravity speed's ceiling, in cruise speeds.")]
        [SerializeField, Range(0f, 10f)] float gravitySpeedCeilingCruises = 4f;

        [Tooltip("Seconds the gravity speed takes to fade once the pair is gone (dpAssistFade).")]
        [SerializeField, Range(0.05f, 10f)] float gravityFadeSeconds = 2f;

        [Header("Domain colour")]
        [Tooltip("How far the pair is drawn toward its owner's domain: the sink's shadow toward the DARK colour, the source's core toward the LIGHT.")]
        [SerializeField, Range(0f, 1f)] float domainTintAmount = 0.7f;

        [Header("What the sink takes")]
        [Tooltip("A share of every element a rival vessel holds that the sink strips when the vessel passes through it, left as crystals on the sink's side. 1 = everything.")]
        [SerializeField, Range(0f, 1f)] float crystalStripShare = 1f;

        [Tooltip("A creature whose centre comes within this many sink horizons is swallowed (killed, its body suctioned into the sink).")]
        [SerializeField, Range(1f, 10f)] float faunaSwallowHorizons = 1.5f;

        [Header("Pathfinder (Time)")]
        [Tooltip("How far ahead the path is run, world units (ftLength).")]
        [SerializeField, Range(50f, 2000f)] float pathLength = 600f;

        [Tooltip("World units between two predicted points (ftStep).")]
        [SerializeField, Range(0.5f, 20f)] float pathStep = 3f;

        [Tooltip("The path starts this far in front of the nose, world units (ftNose).")]
        [SerializeField, Range(0f, 50f)] float pathNoseOffset = 8f;

        [Tooltip("The path ends where it comes back within this of itself (ftMargin)...")]
        [SerializeField, Range(0.5f, 50f)] float loopMargin = 8f;

        [Tooltip("...at least this far further on, world units (ftMinLoop).")]
        [SerializeField, Range(5f, 500f)] float minLoop = 60f;

        [Tooltip("The poles must bend the path at least this much, degrees, to WARP it (ftWarpDeg). A pass through the wormhole always does.")]
        [SerializeField, Range(0f, 90f)] float warpDegrees = 3f;

        [Tooltip("Time is rate: the warp boost, a multiple of the hull's flight clock while the path is warped — 2× at rest, " +
                 "the studio's 3× at level 5, 4× at level 10, floored at 1 (never a slow-down).")]
        [SerializeField] ElementalFloat boost = ElementalFloat.Multiplier(2f, 4f, Element.Time, 1f);

        [Tooltip("How fast the boost arrives, per second (ftRise).")]
        [SerializeField, Range(0.1f, 60f)] float boostRise = 20f;

        [Tooltip("Seconds the boost takes to fade once the path is no longer warped (ftFade).")]
        [SerializeField, Range(0.01f, 5f)] float boostFadeSeconds = 0.6f;

        [Tooltip("Dot diameter on screen, pixels at 1080p (ftDotPx).")]
        [SerializeField, Range(0.5f, 20f)] float dotPixels = 4f;

        [Tooltip("Space between two dots, in dot diameters (ftDotGap).")]
        [SerializeField, Range(0f, 40f)] float dotGap = 10f;

        [Tooltip("The path's colour while it is open (blue-grey).")]
        [SerializeField] Color openColor = new(150f / 255f, 166f / 255f, 194f / 255f, 1f);

        [Tooltip("The path's colour while the poles warp it (lime).")]
        [SerializeField] Color warpedColor = new(157f / 255f, 1f, 46f / 255f, 1f);

        [Header("Autopilot")]
        [Tooltip("The autopilot's squeeze on each trigger it pulls, 0..1. The studio's aiWarpQ: 1 (a full squeeze) " +
                 "measured fastest on every course.")]
        [SerializeField, Range(0f, 1f)] float autopilotHold01 = 1f;

        [Tooltip("The autopilot lays a pair only when its target is at least this far away, world units.")]
        [SerializeField, Min(0f)] float autopilotMinDistance = 300f;

        [Tooltip("...and holds it at most this long before letting go to look again, seconds.")]
        [SerializeField, Range(0.5f, 20f)] float autopilotHoldSeconds = 4f;

        [Tooltip("Seconds between two of the autopilot's pairs.")]
        [SerializeField, Range(0f, 20f)] float autopilotIntervalSeconds = 2f;

        [Header("Autopilot - watching the path (the studio's measured best)")]
        [Tooltip("On: the autopilot holds the poles while they WARP its path (the pathfinder's lime: the flight runs " +
                 "faster), lets go as its target comes close, and lays the next pair almost at once. Off: the timed " +
                 "hold above (Autopilot Hold Seconds). Measured in the Stoat Flight Studio: Hard went from 70-89 s to " +
                 "49-59 s over the four courses, warped 79-95% of the race (Docs/Studios/VesselStudio/README.md).")]
        [SerializeField] bool autopilotWatchPath = true;

        [Tooltip("Watching the path: lay a pair only when the target is further than this, and let go once it is " +
                 "closer, so the bend does not throw the hull past the ring's mouth. World units.")]
        [SerializeField, Range(0f, 400f)] float autopilotLetGoNear = 60f;

        [Tooltip("Watching the path: let go once the path has not been warped for this long, seconds.")]
        [SerializeField, Range(0.05f, 3f)] float autopilotDrySeconds = 0.5f;

        [Tooltip("Watching the path: always hold at least this long before judging the warp, seconds.")]
        [SerializeField, Range(0f, 3f)] float autopilotMinHoldSeconds = 0.8f;

        [Tooltip("Watching the path: never hold one pair longer than this, seconds.")]
        [SerializeField, Range(1f, 60f)] float autopilotMaxHoldSeconds = 15f;

        [Tooltip("Watching the path: seconds between letting go and laying the next pair.")]
        [SerializeField, Range(0f, 5f)] float autopilotRelaySeconds = 0.25f;

        [Header("Autopilot - orbit cap")]
        [Tooltip("The most the autopilot may circle its own sink on one pair, in degrees swept round it; then it lets " +
                 "go and waits Autopilot Interval Seconds before the next pair. A hull circling the sink keeps its " +
                 "path warped every frame, so without this the watching hold only ended at Max Hold Seconds - four " +
                 "to six laps round the hole. 360 = one lap. 0 = no cap.")]
        [SerializeField, Range(0f, 1080f)] float autopilotMaxOrbitDegrees = 360f;

        [Header("Audio (FMOD) — every sound is an exposed, editable field")]
        [Tooltip("Played when a squeeze opens the pair. Ships empty until a sound is chosen.")]
        [SerializeField] EventReference openEvent;

        [Tooltip("Played when the poles meet and annihilate. Ships empty until a sound is chosen.")]
        [SerializeField] EventReference annihilateEvent;

        [Tooltip("Played when the path turns lime and the boost comes on. Ships empty until a sound is chosen.")]
        [SerializeField] EventReference warpEvent;

        public float HoldExponent => holdExponent;
        public float HoldRampSeconds => holdRampSeconds;
        public float AheadDistance => aheadDistance;
        public float SidewaysMax => sidewaysMax;
        public float LengthwaysMax => lengthwaysMax;
        public float FollowRate => followRate;
        public float PoleGM => poleGM;
        public float PoleHorizon => poleHorizon;
        public ElementalFloat PoleSize => poleSize;
        public float SourcePush => sourcePush;
        public float SourceSofteningHorizons => sourceSofteningHorizons;
        public float AccelerationCap => accelerationCap;
        public float SinkGrowSeconds => sinkGrowSeconds;
        public float SourceGrowSeconds => sourceGrowSeconds;
        public float TurnCap => turnCap;
        public float Grip => grip;
        public float GravitySpeedCeilingCruises => gravitySpeedCeilingCruises;
        public float GravityFadeSeconds => gravityFadeSeconds;
        public float DomainTintAmount => domainTintAmount;
        public float CrystalStripShare => crystalStripShare;
        public float FaunaSwallowHorizons => faunaSwallowHorizons;
        public float PathLength => pathLength;
        public float PathStep => pathStep;
        public float PathNoseOffset => pathNoseOffset;
        public float LoopMargin => loopMargin;
        public float MinLoop => minLoop;
        public float WarpDegrees => warpDegrees;
        public ElementalFloat Boost => boost;
        public float BoostRise => boostRise;
        public float BoostFadeSeconds => boostFadeSeconds;
        public float DotPixels => dotPixels;
        public float DotGap => dotGap;
        public Color OpenColor => openColor;
        public Color WarpedColor => warpedColor;
        public float AutopilotHold01 => autopilotHold01;
        public float AutopilotMinDistance => autopilotMinDistance;
        public float AutopilotHoldSeconds => autopilotHoldSeconds;
        public float AutopilotIntervalSeconds => autopilotIntervalSeconds;
        public bool AutopilotWatchPath => autopilotWatchPath;
        public float AutopilotLetGoNear => autopilotLetGoNear;
        public float AutopilotDrySeconds => autopilotDrySeconds;
        public float AutopilotMinHoldSeconds => autopilotMinHoldSeconds;
        public float AutopilotMaxHoldSeconds => autopilotMaxHoldSeconds;
        public float AutopilotRelaySeconds => autopilotRelaySeconds;
        public float AutopilotMaxOrbitDegrees => autopilotMaxOrbitDegrees;
        public EventReference OpenEvent => openEvent;
        public EventReference AnnihilateEvent => annihilateEvent;
        public EventReference WarpEvent => warpEvent;

        /// <summary>The flight settings one step of field flight reads (the prediction and the flight share them).</summary>
        public StoatDipoleMath.FlightSettings Flight(float cruise) => new()
        {
            TurnCap = turnCap, Grip = grip, GravitySpeedCeiling = gravitySpeedCeilingCruises * Mathf.Max(1f, cruise),
        };

        /// <summary>The pathfinder's settings (the wormhole is always open: a dipole's sink is a throat).</summary>
        public StoatDipoleMath.PathSettings Path() => new()
        {
            Length = pathLength, Step = pathStep, NoseOffset = pathNoseOffset, LoopMargin = loopMargin, MinLoop = minLoop,
            WarpDegrees = warpDegrees, ExitGap = BlackHolePairMath.EmitRadiusFraction, PortalOpen = true,
        };
    }
}
