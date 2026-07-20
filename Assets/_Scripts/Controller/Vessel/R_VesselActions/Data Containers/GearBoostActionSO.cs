using System;
using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Boost that climbs through discrete gears while the triggering input is held
    /// (e.g. the Rhino's full-speed-straight run). Each gear engages as an instant
    /// speed step — a drag-race shift kick — never a lerp between levels; the smoothed
    /// cruise speed is snapped straight to the new gear's target by
    /// <see cref="GearBoostActionExecutor"/> via <see cref="VesselTransformer.SnapSpeedToThrottleTarget"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "GearBoostAction", menuName = "ScriptableObjects/Vessel Actions/Gear Boost")]
    public class GearBoostActionSO : ShipActionSO
    {
        [Serializable]
        public struct Gear
        {
            [Tooltip("BoostMultiplier applied while this gear is engaged (scales throttle speed; " +
                     "the TIME elemental multiplier stacks on top in the transformer as usual).")]
            public float Multiplier;

            [Tooltip("Seconds held in this gear before shifting up to the next. Ignored on the " +
                     "top gear, which holds until the input is released.")]
            public float SecondsToNext;
        }

        [Header("Gears")]
        [Tooltip("Gear ladder, bottom to top. First gear engages immediately on input; each " +
                 "subsequent gear engages after the previous gear's SecondsToNext.")]
        [SerializeField]
        Gear[] gears =
        {
            new Gear { Multiplier = 1.75f, SecondsToNext = 1.5f },
            new Gear { Multiplier = 2.5f,  SecondsToNext = 1.5f },
            new Gear { Multiplier = 3.25f, SecondsToNext = 1.5f },
            new Gear { Multiplier = 4f,    SecondsToNext = 0f },
        };

        [Header("Shift Feel")]
        [Tooltip("Extra forward shove on each gear engage, applied as a short decaying velocity " +
                 "modifier on top of the instant speed step. 0 disables the shove.")]
        [SerializeField] float shiftKickSpeed = 10f;

        [Tooltip("Seconds the shift shove takes to decay.")]
        [SerializeField] float shiftKickSeconds = 0.4f;

        [Tooltip("SFX played on each gear engage.")]
        [SerializeField] GameplaySFXCategory shiftSFX = GameplaySFXCategory.SpeedBurst;

        public IReadOnlyList<Gear> Gears => gears;
        public float ShiftKickSpeed => shiftKickSpeed;
        public float ShiftKickSeconds => shiftKickSeconds;
        public GameplaySFXCategory ShiftSFX => shiftSFX;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GearBoostActionExecutor>()?.Begin(this, vesselStatus);

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<GearBoostActionExecutor>()?.End();
    }
}
