using CosmicShore.Core;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Per-frame executor for Manta analog trigger controls.
    /// Reads LeftTriggerAnalog and RightTriggerAnalog each frame and computes:
    ///   - Proportional yaw from the net trigger difference (RT - LT)
    ///   - Proportional boost from the trigger overlap min(LT, RT)
    ///
    /// Example: LT=1.0, RT=0.5 → turn left at 0.5 intensity, boost at 0.5 intensity.
    /// Both triggers fully pulled → full boost, no turn (current behavior preserved).
    ///
    /// Only active when InputDevice is Gamepad. On touch, the event-driven
    /// Yawstery + Boost actions handle control via _gamepadActionOverrides.
    /// </summary>
    public sealed class MantaAnalogTurnBoostExecutor : ShipActionExecutorBase
    {
        [Header("Yaw Response")]
        [Tooltip("Max yaw speed (deg/sec) at full net trigger pull.")]
        [SerializeField] private float maxYawDegPerSec = 60f;

        [Header("Boost")]
        [Tooltip("If > 0, overrides VesselStatus.BoostMultiplier as the base value.")]
        [SerializeField] private float boostMultiplierOverride;

        [Header("Refs")]
        [SerializeField] private VesselTransformer vesselTransformer;

        [Inject] private AudioSystem audioSystem;

        private const float TriggerDeadzone = 0.05f;

        private IVesselStatus _status;
        private float _baseBoostMultiplier;
        private bool _wasBoosting;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _baseBoostMultiplier = boostMultiplierOverride > 0f
                ? boostMultiplierOverride
                : shipStatus.BoostMultiplier;

            if (vesselTransformer == null)
                vesselTransformer = shipStatus.VesselTransformer;
        }

        private void Update()
        {
            if (_status == null || vesselTransformer == null) return;
            if (_status.InputStatus == null) return;
            if (_status.InputStatus.ActiveInputDevice != InputDeviceType.Gamepad) return;
            if (_status.IsStationary) return;

            float lt = _status.InputStatus.LeftTriggerAnalog;
            float rt = _status.InputStatus.RightTriggerAnalog;

            if (lt < TriggerDeadzone) lt = 0f;
            if (rt < TriggerDeadzone) rt = 0f;

            // Net turn: positive = right, negative = left
            float rawTurn = rt - lt;

            // Boost intensity: overlap of both triggers
            float boostIntensity = Mathf.Min(lt, rt);

            // Apply yaw
            if (Mathf.Abs(rawTurn) > 0.001f && !_status.IsTranslationRestricted)
            {
                float yawDeg = rawTurn * maxYawDegPerSec * Time.deltaTime;
                vesselTransformer.ApplyRotation(yawDeg, _status.Transform.up);
            }

            // Apply analog boost
            if (boostIntensity > 0.01f)
            {
                if (!_wasBoosting && audioSystem != null)
                    audioSystem.PlayGameplaySFX(GameplaySFXCategory.BoostActivate);

                _status.IsBoosting = true;
                _status.IsStationary = false;
                _status.BoostMultiplier = 1f + (_baseBoostMultiplier - 1f) * boostIntensity;
                _wasBoosting = true;
            }
            else if (_wasBoosting)
            {
                _status.IsBoosting = false;
                _status.BoostMultiplier = _baseBoostMultiplier;
                _wasBoosting = false;
            }
        }

        private void OnDisable()
        {
            if (_wasBoosting && _status != null)
            {
                _status.IsBoosting = false;
                _status.BoostMultiplier = _baseBoostMultiplier;
                _wasBoosting = false;
            }
        }
    }
}
