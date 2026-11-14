using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number of the Jewel Wasp (vessel class 31; design <c>R_VesselActions/JEWEL_WASP.md</c>; Garrett, 2026-11-14).
    /// One parameter per element (the fleet's rule): Charge the attack's cooldown, Mass how fast a possessed plant grows,
    /// Space how far a host's suction reaches, Time how fast a host swims.
    /// </summary>
    [CreateAssetMenu(fileName = "JewelWaspConfig", menuName = "ScriptableObjects/Vessel Actions/Jewel Wasp Config")]
    public sealed class JewelWaspConfigSO : ScriptableObject
    {
        [Header("The sting (RT at fauna, LT at flora)")]
        [Tooltip("How far off the aim a creature or plant can be and still be the one highlighted (degrees).")]
        [SerializeField, Range(1f, 45f)] float aimConeDegrees = 14f;
        [Tooltip("Sting range when the wasp is in no cell (u). In a cell the range is unlimited: the cell's diameter " +
                 "(Garrett, 2026-11-14: \"make the sting range unlimited (cell diameter)\").")]
        [SerializeField, Min(10f)] float stingRangeOutsideCell = 3000f;
        [Tooltip("How fast the wasp flies to the host it stung (u/s).")]
        [SerializeField, Min(10f)] float stingSpeed = 1400f;
        [Tooltip("The sting itself on arrival: the wasp tucks in beside the heart and stings it (s).")]
        [SerializeField, Min(0.05f)] float stingSeconds = 0.35f;
        [Tooltip("The wait between one sting and the next (s). Time level 5, Restless, makes it 0.")]
        [SerializeField, Min(0f)] float stingCooldown = 0.6f;
        [Tooltip("A sting gives up when its host is still this far after this long (s): it fled or died.")]
        [SerializeField, Min(0.5f)] float stingMaxSeconds = 4f;

        [Header("The hull")]
        [Tooltip("The hull model's scale flying free (1 = the stand-in airframe's own size).")]
        [SerializeField, Min(0.01f)] float hullScale = 0.45f;
        [Tooltip("The hull model's scale nested beside a host's heart, so it tucks in beside the crystal: an onlooker " +
                 "sees a creature, and only an observant one sees the wasp.")]
        [SerializeField, Min(0.01f)] float nestedScale = 0.22f;
        [Tooltip("How far beside the heart the wasp nests, in heart radii.")]
        [SerializeField, Min(0f)] float nestOffset = 1.3f;
        [Tooltip("The heart's radius when the host carries no crystal (u).")]
        [SerializeField, Min(0.1f)] float fallbackHeartRadius = 2f;

        [Header("Flying a host (Time)")]
        [Tooltip("How fast a host turns under the sticks: pitch and yaw (degrees per second at full stick).")]
        [SerializeField, Min(1f)] float hostTurnDegrees = 120f;
        [Tooltip("How fast a host rolls under the sticks (degrees per second at full stick).")]
        [SerializeField, Min(0f)] float hostRollDegrees = 110f;
        [Tooltip("A host's speed, as a multiple of its own top speed: Time's parameter (Quicken). 1.5 at rest, 3 at Time 10.")]
        [SerializeField] ElementalFloat hostSpeedScale = ElementalFloat.Multiplier(1.5f, 3f, Element.Time, 1f);
        [Tooltip("The share of a host's speed it keeps with the sticks together (slowest); apart is full speed.")]
        [SerializeField, Range(0f, 1f)] float hostSlowShare = 0.45f;
        [Tooltip("THE TIME ABILITY in a host (LT): a burst of speed, this many times the host's speed.")]
        [SerializeField, Min(1f)] float burstMultiplier = 2.2f;
        [Tooltip("How long a burst lasts (s).")]
        [SerializeField, Min(0.1f)] float burstSeconds = 1.2f;
        [Tooltip("The wait between bursts (s). Time level 5, Restless, makes it 0.")]
        [SerializeField, Min(0f)] float burstCooldown = 3f;

        [Header("Suction (Space, RT at prisms)")]
        [Tooltip("How far a host's suction reaches, as a multiple of its own feeding reach: Space's parameter. x1 at rest, " +
                 "x3 at Space 10 (Garrett, 2026-11-14: \"it boosts the range of the fauna's suction effect similar to the " +
                 "time boosting speed\").")]
        [SerializeField] ElementalFloat suctionReachScale = ElementalFloat.Multiplier(1f, 3f, Element.Space, 0.5f);
        [Tooltip("Prisms one mouthful takes at most.")]
        [SerializeField, Range(1, 64)] int suctionBites = 8;
        [Tooltip("The wait between mouthfuls while RT is held (s).")]
        [SerializeField, Min(0.05f)] float suctionInterval = 0.45f;

        [Header("The attack (Charge, RT at a vessel)")]
        [Tooltip("The wait between attacks (s): Charge's parameter, 3 s at rest to 0.8 s at Charge 10 (Garrett, 2026-11-14: " +
                 "\"charge should control the cooldown of the fauna attack that steals the petals\").")]
        [SerializeField] ElementalFloat attackCooldown = ElementalFloat.Multiplier(3f, 0.8f, Element.Charge, 0.4f);
        [Tooltip("How long the attack animation plays (s); the strike lands at FaunaAttackDriver.StrikeAt of it.")]
        [SerializeField, Min(0.1f)] float attackSeconds = 0.6f;
        [Tooltip("How far the attack reaches beyond the host's body (u).")]
        [SerializeField, Min(0f)] float attackReach = 22f;
        [Tooltip("RT aims at a rival vessel instead of prisms when one is within this many attack reaches ahead.")]
        [SerializeField, Min(1f)] float attackAimReaches = 3f;
        [Tooltip("The scored hit an attack lands (PvP is petals only): Strike, the fleet's contact class.")]
        [SerializeField] CombatHitClass attackHitClass = CombatHitClass.Strike;

        [Header("Leaving (Charge, A)")]
        [Tooltip("How fast the wasp leaves its host (u/s), along the host's heading.")]
        [SerializeField, Min(0f)] float leaveSpeed = 90f;
        [Tooltip("Seconds the wasp stays untouchable after it leaves, so it is not jousted by the host it was riding.")]
        [SerializeField, Min(0f)] float leaveGraceSeconds = 0.6f;
        [Tooltip("CHARGE LEVEL 5, Burst: leaving bursts the host, a blast-class scored hit on every rival vessel within " +
                 "this radius (u).")]
        [SerializeField, Min(0f)] float burstRadius = 60f;
        [Tooltip("The scored hit a Burst lands.")]
        [SerializeField] CombatHitClass burstHitClass = CombatHitClass.MissileBlast;

        [Header("A plant (Mass, LT at flora)")]
        [Tooltip("How much faster a possessed plant grows: Mass's parameter, x2 at rest to x6 at Mass 10 (Garrett, " +
                 "2026-11-14: \"possessing flora should cause them to grow based on your mass level while in possession\").")]
        [SerializeField] ElementalFloat floraGrowth = ElementalFloat.Multiplier(2f, 6f, Element.Mass, 1.2f);
        [Tooltip("MASS LEVEL 5, Overgrowth: every plant within this radius of the one possessed grows as fast (u).")]
        [SerializeField, Min(0f)] float overgrowthRadius = 200f;
        [Tooltip("How far the camera stands back while the wasp rides a plant (u): far, so the next host is easy to pick.")]
        [SerializeField, Min(10f)] float floraCameraDistance = 650f;
        [Tooltip("How fast the sticks turn the camera round a plant (degrees per second at full stick).")]
        [SerializeField, Min(1f)] float floraOrbitDegrees = 100f;

        [Header("The camera on a host")]
        [Tooltip("The chase camera stands back this many host body radii, plus the base below.")]
        [SerializeField, Min(0f)] float hostCameraBodies = 6f;
        [Tooltip("The chase camera's base stand-back on a host (u).")]
        [SerializeField, Min(0f)] float hostCameraBase = 30f;
        [Tooltip("How fast the camera eases to a new stand-back (1/s).")]
        [SerializeField, Min(0.1f)] float cameraEase = 4f;

        [Header("The omni crystal")]
        [Tooltip("The predators an omni crystal can hatch (Garrett, 2026-11-14: \"for the omni make a random predator\"); the " +
                 "wasp possesses it at once.")]
        [SerializeField] List<FaunaConfigurationSO> omniPredators = new();

        [Header("The AI")]
        [Tooltip("An AI wasp rides a host this long before it looks for another (s).")]
        [SerializeField, Min(1f)] float aiHostSeconds = 14f;
        [Tooltip("An AI wasp looks for a host this often while free (s).")]
        [SerializeField, Min(0.1f)] float aiLookSeconds = 1.5f;

        [Header("Events")]
        [Tooltip("Drag Event_CombatHitStats.asset - the channel StatsManager scores hits on. Every attack and Burst is a " +
                 "scored hit (PvP is petals only).")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        [Header("Audio (empty slots ship silent - never a borrowed event)")]
        [SerializeField] FMODUnity.EventReference stingEvent;
        [SerializeField] FMODUnity.EventReference nestEvent;
        [SerializeField] FMODUnity.EventReference leaveEvent;
        [SerializeField] FMODUnity.EventReference attackEvent;
        [SerializeField] FMODUnity.EventReference suctionEvent;
        [SerializeField] FMODUnity.EventReference burstEvent;

        public float AimConeDegrees => aimConeDegrees;
        public float StingRangeOutsideCell => stingRangeOutsideCell;
        public float StingSpeed => stingSpeed;
        public float StingSeconds => stingSeconds;
        public float StingCooldown => stingCooldown;
        public float StingMaxSeconds => stingMaxSeconds;
        public float HullScale => hullScale;
        public float NestedScale => nestedScale;
        public float NestOffset => nestOffset;
        public float FallbackHeartRadius => fallbackHeartRadius;
        public float HostTurnDegrees => hostTurnDegrees;
        public float HostRollDegrees => hostRollDegrees;
        public ElementalFloat HostSpeedScale => hostSpeedScale;
        public float HostSlowShare => hostSlowShare;
        public float BurstMultiplier => burstMultiplier;
        public float BurstSeconds => burstSeconds;
        public float BurstCooldown => burstCooldown;
        public ElementalFloat SuctionReachScale => suctionReachScale;
        public int SuctionBites => suctionBites;
        public float SuctionInterval => suctionInterval;
        public ElementalFloat AttackCooldown => attackCooldown;
        public float AttackSeconds => attackSeconds;
        public float AttackReach => attackReach;
        public float AttackAimReaches => attackAimReaches;
        public CombatHitClass AttackHitClass => attackHitClass;
        public float LeaveSpeed => leaveSpeed;
        public float LeaveGraceSeconds => leaveGraceSeconds;
        public float BurstRadius => burstRadius;
        public CombatHitClass BurstHitClass => burstHitClass;
        public ElementalFloat FloraGrowth => floraGrowth;
        public float OvergrowthRadius => overgrowthRadius;
        public float FloraCameraDistance => floraCameraDistance;
        public float FloraOrbitDegrees => floraOrbitDegrees;
        public float HostCameraBodies => hostCameraBodies;
        public float HostCameraBase => hostCameraBase;
        public float CameraEase => cameraEase;
        public IReadOnlyList<FaunaConfigurationSO> OmniPredators => omniPredators;
        public float AIHostSeconds => aiHostSeconds;
        public float AILookSeconds => aiLookSeconds;
        public ScriptableEventCombatHitStats OnCombatHitLanded => onCombatHitLanded;
        public FMODUnity.EventReference StingEvent => stingEvent;
        public FMODUnity.EventReference NestEvent => nestEvent;
        public FMODUnity.EventReference LeaveEvent => leaveEvent;
        public FMODUnity.EventReference AttackEvent => attackEvent;
        public FMODUnity.EventReference SuctionEvent => suctionEvent;
        public FMODUnity.EventReference BurstEvent => burstEvent;
    }
}
