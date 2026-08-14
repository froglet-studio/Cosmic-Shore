using UnityEngine;
using CosmicShore.Data;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine.Serialization;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using System.Linq;

namespace CosmicShore.Gameplay
{
public class VesselTransformer : MonoBehaviour
{
    protected const float LERP_AMOUNT = 1.5f;

    [SerializeField] protected bool toggleManualThrottle;
    [SerializeField] protected bool decayBoost = false;
    [SerializeField] float MaxBoostMultiplier = 5f;
    [SerializeField] float BoostDecayRate = 0.1f;

    [Tooltip("Collapse drift onto a single analog trigger: the left trigger's 0-1 travel is " +
             "remapped across the full no-drift → single → sharp range, and the right trigger no " +
             "longer feeds drift (freed for another ability, e.g. the Squirrel's tube). Leave off " +
             "for the default two-trigger drift where both triggers sum (e.g. Manta).")]
    [SerializeField] bool singleTriggerDrift = false;

    [Tooltip("Hold the cruise speed the vessel carried INTO a drift for the drift's whole " +
             "duration: the throttle stops feeding speed the moment the drift starts, and the " +
             "latched value is flown until it ends. Combined with the course lock (drift damping " +
             "0) that makes a drift a pure change of HEADING — velocity direction and magnitude " +
             "both frozen, i.e. a momentum-preserving slide (the Dolphin). Leave off for the " +
             "legacy drift, where the throttle keeps driving speed while drifting (the Squirrel, " +
             "whose racing drift is throttle-modulated).")]
    [SerializeField] bool holdSpeedWhileDrifting = false;

    [HideInInspector] public float DriftDamping = 0f;

    [Header("Events")]
    [SerializeField] private ScriptableEventBoostChanged boostChanged;

    #region Vessel
    protected IVessel Vessel;
    protected IVesselStatus VesselStatus => Vessel?.VesselStatus;
    protected ResourceSystem ResourceSystem => VesselStatus?.ResourceSystem;
    #endregion

        protected IInputStatus InputStatus => VesselStatus?.InputStatus;

        protected float speed;
        protected Quaternion accumulatedRotation;

        [HideInInspector] public float MinimumSpeed;
        [HideInInspector] public float ThrottleScaler;

        public float DefaultMinimumSpeed = 10f;
        public float DefaultThrottleScaler = 50f;
        public ElementalFloat ThrottleScalerMultiplier = new(1f);

        public float PitchScaler = 130f;
        public float YawScaler = 130f;
        public float RollScaler = 130f;
        public float RotationThrottleScaler = 0f;

        [Tooltip("Pitch and yaw rate multiplier while IsTranslationRestricted (the stationary / " +
                 "turret stance). Stopped, the vessel is an aiming platform rather than a flying " +
                 "one, so it swings onto targets faster. Applies to the WHOLE rate — the " +
                 "throttle-derived term as well as the Pitch/Yaw scaler. ROLL is deliberately " +
                 "not scaled. 1 = no change while stopped.")]
        [SerializeField, Min(0f)] float restrictedTurnMultiplier = 3f;

        /// <summary>Pitch/yaw rate scalar for this frame — <c>restrictedTurnMultiplier</c> while
        /// the vessel is translation-restricted, 1 otherwise. Read at use time (the stance is
        /// toggled mid-flight), and applied by both this class's Pitch/Yaw and the overrides in
        /// <see cref="SingleStickVesselTransformer"/>, which is what the Sparrow and Serpent
        /// actually run — a base-only change would not reach either of them.</summary>
        protected float TurnScalar =>
            VesselStatus != null && VesselStatus.IsTranslationRestricted ? restrictedTurnMultiplier : 1f;

        private readonly List<ShipThrottleModifier> ThrottleModifiers = new();
        private readonly List<ShipVelocityModifier> VelocityModifiers = new();

        private float speedModifierMax = 6f;
        private float velocityModifierMax = 100f;
        protected float throttleMultiplier = 1f;
        public float SpeedMultiplier => throttleMultiplier;

        protected Vector3 velocityShift = Vector3.zero;

        // Tracks whether the body flare is currently raised, so the rest-state material write
        // happens on the transition instead of every frame. Starts true so the first
        // ApplyVelocityModifiers pass normalizes the material once, as it always did.
        bool _bodyFlaring = true;

        /// <summary>Current additive world-space displacement (the ModifyVelocity channel),
        /// summed on top of speed * Course by MoveShip. Read-only view for systems that need
        /// the vessel's ACTUAL travel direction (e.g. barrel-roll bridging prisms).</summary>
        public Vector3 VelocityShift => velocityShift;

        /// <summary>When set, trail prisms orient along this rotation instead of the vessel's
        /// facing. Owned by the barrel-roll controller for the roll duration; null restores
        /// normal facing-aligned trail.</summary>
        public Quaternion? BlockRotationOverride { get; set; }
        private bool isActive;

        // ----------------------------- Analog Drift -----------------------------
        private Vector3 _driftBaseRotations;
        private bool _hasDriftBase;
        private bool _singleDriftActive;
        private bool _sharpDriftActive;
        private bool _singleDriftParamsSet;
        private bool _sharpDriftParamsSet;
        private float _singleDriftRotMult = 1f;
        private float _singleDriftDamp;
        private float _sharpDriftRotMult = 1f;
        private float _sharpDriftDamp;
        private float _frameTriggerSum;
        private bool _driftEaseOutPending;
        private const float DRIFT_EASE_SPEED = 12f; // ~83ms for 0→1 ramp
        public bool IsDriftActive => _singleDriftActive || _sharpDriftActive || _driftEaseOutPending;

        private bool _driftSpeedHeld;
        private float _heldDriftSpeed;

        /// <summary>True while <see cref="holdSpeedWhileDrifting"/> is pinning the cruise speed
        /// to the value the vessel carried into the current drift. Read by the MoveShip
        /// overrides so the manual-throttle channel is disabled alongside the throttle target
        /// (see <see cref="RefreshDriftSpeedHold"/>).</summary>
        public bool IsDriftSpeedHeld => _driftSpeedHeld;

        // ----------------------------- Update Loop -----------------------------
        protected virtual void Update()
        {
            if (!isActive || VesselStatus == null || VesselStatus.IsStationary)
                return;

            // Trail prisms orient by blockRotation (facing). During velocity≠forward states
            // (barrel roll) the roll controller overrides it with the actual travel
            // direction so bridging prisms follow the true path — replicates for free via
            // the owner-written n_BlockRotation.
            VesselStatus.blockRotation = BlockRotationOverride ?? transform.rotation;

            if (decayBoost) DecayBoost();

            // Smooth trigger sum for non-analog input to simulate a quick trigger pull
            float rawTriggerSum = GetTriggerSum();
            bool needsEasing = InputStatus != null
                            && InputStatus.ActiveInputDevice != InputDeviceType.Gamepad;
            _frameTriggerSum = needsEasing
                ? Mathf.MoveTowards(_frameTriggerSum, rawTriggerSum, DRIFT_EASE_SPEED * Time.deltaTime)
                : rawTriggerSum;

            // Finish deferred ease-out once the smoothed value decays to zero
            if (_driftEaseOutPending && _frameTriggerSum < 0.01f)
            {
                _frameTriggerSum = 0f;
                _driftEaseOutPending = false;
                RestoreDriftBase();
                if (VesselStatus != null)
                    VesselStatus.IsDrifting = false;
            }

            ApplyAnalogDrift();
            RotateShip();
        
            if (VesselStatus.IsTranslationRestricted)
            {
                // Restricted stance: no throttle, no course travel. Velocity modifiers still
                // AGE here (previously they froze mid-flight and lurched out the instant the
                // stance was released), but only those flagged ignoresTranslationRestriction
                // actually displace — today just the Sparrow's strafing-roll dodge.
                ApplyVelocityModifiers(translationRestricted: true);
                MoveRestricted();
                return;
            }

            ApplyThrottleModifiers();
            ApplyVelocityModifiers();
            MoveShip();
        }

        /// <summary>Position update while <c>IsTranslationRestricted</c>: throttle and course
        /// travel are off, so the only displacement is the exempt ModifyVelocity channel (see
        /// <see cref="ShipVelocityModifier.ignoresTranslationRestriction"/>). Deliberately does
        /// NOT write <c>VesselStatus.Speed</c> or <c>Course</c> — a restricted vessel's reported
        /// speed/heading is unchanged from before this branch existed, so nothing downstream
        /// (gun velocity inheritance, telemetry, the speed tunnel) shifts behaviour.</summary>
        protected virtual void MoveRestricted()
        {
            if (velocityShift.sqrMagnitude <= 0f) return;
            transform.position += velocityShift * Time.deltaTime;
        }

        protected virtual void DecayBoost()
        {
            if (VesselStatus == null) return;

                // Decay toward 1.0
        
            VesselStatus.BoostMultiplier = VesselStatus.BoostMultiplier > 1 ? 
                    VesselStatus.BoostMultiplier - BoostDecayRate * Time.deltaTime:
                    Mathf.Min(1f, VesselStatus.BoostMultiplier + BoostDecayRate * Time.deltaTime);

            boostChanged?.Raise(new BoostChangedPayload
            {
                BoostMultiplier = VesselStatus.BoostMultiplier,
                MaxMultiplier = MaxBoostMultiplier,
                SourceDomain = Domains.Blue,
                VesselStatus = VesselStatus
            });
        }

        // ----------------------------- Initialization -----------------------------
        public virtual void Initialize(IVessel vessel)
        {
            Vessel = vessel;
            // ResetTransformer();
        }
    
        public void ToggleActive(bool active) => isActive = active;

        // ----------------------------- Reset State -----------------------------
        public void ResetTransformer()
        {
            // Core speed/rotation
            MinimumSpeed = DefaultMinimumSpeed;
            ThrottleScaler = DefaultThrottleScaler;
            speed = 0f;
            throttleMultiplier = 1f;
            _speedTrackingRate = 0f;

            // Rotation - reset to face forward
            accumulatedRotation = Quaternion.identity;
            transform.rotation = Quaternion.identity;

            // Movement
            velocityShift = Vector3.zero;
            _bodyFlaring = true;   // force one rest-state material write on the next pass

            // Drift
            _singleDriftActive = false;
            _sharpDriftActive = false;
            _singleDriftParamsSet = false;
            _sharpDriftParamsSet = false;
            _driftEaseOutPending = false;
            _driftSpeedHeld = false;
            _heldDriftSpeed = 0f;
            RestoreDriftBase();
            _singleDriftRotMult = 1f;
            _singleDriftDamp = 0f;
            _sharpDriftRotMult = 1f;
            _sharpDriftDamp = 0f;

            // Remove lingering modifiers and states
            ThrottleModifiers.Clear();
            VelocityModifiers.Clear();
        }

        // ----------------------------- Rotation Logic -----------------------------
        protected virtual void RotateShip()
        {
            // Apply rotational inputs
            Roll();
            Yaw();
            Pitch();

            if (InputStatus != null && InputStatus.IsGyroEnabled)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    accumulatedRotation * InputStatus.GetGyroRotation(),
                    LERP_AMOUNT * Time.deltaTime);
            }
            else
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    accumulatedRotation,
                    LERP_AMOUNT * Time.deltaTime);
            }
        }

        // ----------------------------- Public Controls -----------------------------
        public void SetPose(Pose pose)
        {
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            accumulatedRotation = pose.rotation;
        }

        /// <summary>
        /// Seed the smoothed cruise speed so a freshly-spawned vessel continues at an inherited
        /// velocity instead of the post-<see cref="ResetTransformer"/> dead stop (speed = 0). Used
        /// by the menu vessel swap so the new ship keeps the previous ship's speed; MoveShip then
        /// eases from here toward the current throttle target as normal.
        /// </summary>
        public void SetInitialSpeed(float initialSpeed) => speed = initialSpeed;

        public void FlatSpinShip(float YAngle)
        {
            accumulatedRotation = Quaternion.AngleAxis(180, transform.up) * accumulatedRotation;
        }

        public void SpinShip(Vector3 newDirection)
        {
            if (SafeLookRotation.TryGet(newDirection, out var rotation, this, logError: false))
                accumulatedRotation = rotation;
        }

        public void GentleSpinShip(Vector3 newDirection, Vector3 newUp, float amount)
        {
            if (SafeLookRotation.TryGet(newDirection, newUp, out var rotation, this, logError: false))
                accumulatedRotation = Quaternion.Slerp(accumulatedRotation, rotation, amount);
        }

        public void ApplyRotation(float angle, Vector3 axis)
        {
            accumulatedRotation = Quaternion.AngleAxis(angle, axis) * accumulatedRotation;
        }

        // ----------------------------- Analog Drift Logic -----------------------------
        /// <summary>
        /// Called by DriftActionSO to register drift parameters. Saves base rotation
        /// scalers on the first call and stores the target multiplier/damping.
        /// </summary>
        public void BeginDrift(float rotMult, float dampTarget, bool isSharp)
        {
            _driftEaseOutPending = false;

            if (!_hasDriftBase)
            {
                _driftBaseRotations = new Vector3(PitchScaler, YawScaler, RollScaler);
                _hasDriftBase = true;
            }

            if (isSharp)
            {
                _sharpDriftRotMult = rotMult;
                _sharpDriftDamp = dampTarget;
                _sharpDriftActive = true;
                _sharpDriftParamsSet = true;
            }
            else
            {
                _singleDriftRotMult = rotMult;
                _singleDriftDamp = dampTarget;
                _singleDriftActive = true;
                _singleDriftParamsSet = true;
            }

            RefreshDriftSpeedHold();
        }

        /// <summary>
        /// Called by DriftActionSO when a drift level ends. Drift params persist for
        /// analog interpolation; only the active flag is cleared. Base rotations are
        /// restored only when all drift levels are inactive.
        /// </summary>
        public void EndDrift(bool isSharp)
        {
            if (isSharp)
                _sharpDriftActive = false;
            else
                _singleDriftActive = false;

            // Release the speed hold on the RELEASE, not at the end of the non-gamepad course
            // ease-out: letting go of the drift is what hands the throttle back, and on the
            // Dolphin that same instant starts the boost discharge, which needs to be able to
            // accelerate the vessel immediately.
            RefreshDriftSpeedHold();

            if (!_singleDriftActive && !_sharpDriftActive)
            {
                bool needsEasing = InputStatus != null
                                && InputStatus.ActiveInputDevice != InputDeviceType.Gamepad;
                if (needsEasing)
                    _driftEaseOutPending = true;
                else
                    RestoreDriftBase();
            }
        }

        /// <summary>
        /// Latch or release the drift speed hold from the live drift-tier flags. The capture
        /// happens on the RISING edge only, so a second tier engaging mid-drift (the Squirrel's
        /// sharp tier stacking onto single) can never re-latch a speed the pilot has already
        /// drifted into, and one tier ending while another still runs leaves the original
        /// captured value in place.
        /// </summary>
        private void RefreshDriftSpeedHold()
        {
            bool shouldHold = holdSpeedWhileDrifting && (_singleDriftActive || _sharpDriftActive);
            if (shouldHold == _driftSpeedHeld) return;

            _driftSpeedHeld = shouldHold;
            if (!shouldHold) return;

            // The throttle is disabled from here, so the captured value has to reproduce the
            // vessel's ACTUAL cruise output - including the manual-throttle scaling MoveShip is
            // about to stop applying (no shipped vessel enables toggleManualThrottle today; this
            // keeps the hold honest if one ever does). `throttleMultiplier` is deliberately NOT
            // folded in: impact throttle modifiers stay live through the drift, so a danger prism
            // still slows a drifting vessel exactly as it slows a flying one.
            _heldDriftSpeed = toggleManualThrottle && InputStatus != null
                ? speed * Mathf.Clamp01(InputStatus.Throttle)
                : speed;
        }

        private void RestoreDriftBase()
        {
            if (!_hasDriftBase) return;
            PitchScaler = _driftBaseRotations.x;
            YawScaler = _driftBaseRotations.y;
            RollScaler = _driftBaseRotations.z;
            DriftDamping = 0f;
            _hasDriftBase = false;
        }

        /// <summary>
        /// Returns the analog drift intensity (0-2). With the default two-trigger drift
        /// (e.g. Manta) both analog triggers sum, so one trigger reaches 1 (single drift) and
        /// both reach 2 (sharp). With <see cref="singleTriggerDrift"/> on (the Squirrel, whose
        /// right trigger is repurposed for the tube ability), only the left trigger feeds drift
        /// and its 0-1 travel is remapped across the full 0-2 range so a single trigger spans
        /// no-drift → single → sharp. For non-gamepad input, returns a binary value based on
        /// which drift level is active.
        /// </summary>
        private float GetTriggerSum()
        {
            if (InputStatus == null)
                return 0f;

            if (InputStatus.ActiveInputDevice == InputDeviceType.Gamepad)
                return singleTriggerDrift
                    ? InputStatus.LeftTriggerAnalog * 2f
                    : InputStatus.LeftTriggerAnalog + InputStatus.RightTriggerAnalog;

            // Non-gamepad fallback: binary intensity
            if (_sharpDriftActive) return 2f;
            if (_singleDriftActive) return 1f;
            return 0f;
        }

        /// <summary>
        /// Applies drift rotation scaling and damping each frame proportional to
        /// the trigger intensity. Non-analog inputs are smoothed via MoveTowards
        /// in Update() to simulate a quick human trigger pull.
        /// </summary>
        private void ApplyAnalogDrift()
        {
            if (!_hasDriftBase || VesselStatus == null || (!VesselStatus.IsDrifting && !_driftEaseOutPending))
                return;

            float triggerSum = _frameTriggerSum;

            // Determine which drift params to use, falling back to whichever tier has been configured
            float singleMult = _singleDriftParamsSet ? _singleDriftRotMult : _sharpDriftRotMult;
            float sharpMult = _sharpDriftParamsSet ? _sharpDriftRotMult : singleMult;
            float singleDamp = _singleDriftParamsSet ? _singleDriftDamp : _sharpDriftDamp;
            float sharpDamp = _sharpDriftParamsSet ? _sharpDriftDamp : singleDamp;

            float effectiveMult;
            float effectiveDamp;

            // Damping is inverted: higher value = course follows forward faster = less drift.
            // At triggerSum 0 we want full damping (no drift feel), ramping down toward the
            // configured values as triggers are pulled.
            const float noDriftDamp = 1f;

            if (triggerSum <= 1f)
            {
                // 0→1: no drift → full single drift
                effectiveMult = Mathf.Lerp(1f, singleMult, triggerSum);
                effectiveDamp = Mathf.Lerp(noDriftDamp, singleDamp, triggerSum);
            }
            else
            {
                // 1→2: full single drift → full sharp drift
                float t = triggerSum - 1f;
                effectiveMult = Mathf.Lerp(singleMult, sharpMult, t);
                effectiveDamp = Mathf.Lerp(singleDamp, sharpDamp, t);
            }

            PitchScaler = _driftBaseRotations.x * effectiveMult;
            YawScaler = _driftBaseRotations.y * effectiveMult;
            RollScaler = _driftBaseRotations.z * effectiveMult;
            DriftDamping = effectiveDamp;
        }

        // ----------------------------- Movement Logic -----------------------------
        protected virtual void Pitch()
        {
            if (InputStatus == null) return;
            accumulatedRotation = Quaternion.AngleAxis(
                InputStatus.YSum * (speed * RotationThrottleScaler + PitchScaler) * TurnScalar * Time.deltaTime,
                transform.right) * accumulatedRotation;
        }

        protected virtual void Yaw()
        {
            if (InputStatus == null) return;
            accumulatedRotation = Quaternion.AngleAxis(
                InputStatus.XSum * (speed * RotationThrottleScaler + YawScaler) * TurnScalar * Time.deltaTime,
                transform.up) * accumulatedRotation;
        }

        protected virtual void Roll()
        {
            if (InputStatus == null) return;
            accumulatedRotation = Quaternion.AngleAxis(
                InputStatus.YDiff * (speed * RotationThrottleScaler + RollScaler) * Time.deltaTime,
                transform.forward) * accumulatedRotation;
        }

        protected float CurrentBoostAmount()
        {
            float boostAmount = 1f;
            if (VesselStatus.IsBoosting)
                // TIME → boost speed: scaled by the vessel's live Time level via its
                // ElementalAbilityMapSO (1x for vessels without a map or Time entry).
                boostAmount = VesselStatus.BoostMultiplier
                              * VesselStatus.ElementalAbilityHandler.Multiplier(Element.Time);

            if (VesselStatus.IsChargedBoostDischarging)
                boostAmount *= VesselStatus.ChargedBoostCharge;

            return boostAmount;
        }

        /// <summary>The steady-state cruise speed the smoothed `speed` field is moving toward
        /// this frame — throttle × boost + minimum. Single source of the formula for
        /// <see cref="AdvanceSpeed"/> in every transformer.</summary>
        protected virtual float ComputeThrottleTarget()
            => InputStatus.XDiff * ThrottleScaler * ThrottleScalerMultiplier.EvaluateLive(VesselStatus) * CurrentBoostAmount()
               + MinimumSpeed;

        float _speedTrackingRate;

        /// <summary>Put the cruise speed into constant-rate tracking: instead of the default
        /// exponential lerp, speed moves toward the throttle target at a fixed
        /// <paramref name="unitsPerSecond"/> — a linear ramp with a steady, readable slope.
        /// Used by ramp boosts (e.g. the Rhino's full-speed-straight run) for constant
        /// acceleration up and, with a higher rate, the fast return down after release.
        /// Auto-reverts to the normal smoothing once the speed lands on the target.</summary>
        public void SetSpeedTrackingRate(float unitsPerSecond)
            => _speedTrackingRate = Mathf.Max(0f, unitsPerSecond);

        /// <summary>Advance the smoothed cruise speed one frame toward
        /// <paramref name="target"/> — constant-rate while a tracking rate is set
        /// (see <see cref="SetSpeedTrackingRate"/>), exponential lerp otherwise.</summary>
        protected void AdvanceSpeed(float target)
        {
            // A held drift pins the smoothed cruise speed at the value the vessel carried in:
            // the caller still computes a throttle target, it simply never reaches `speed` -
            // which IS what "the throttle is disabled during the drift" means mechanically.
            // This lives here rather than in ComputeThrottleTarget because AdvanceSpeed is the
            // one path every transformer's MoveShip runs through, so a subclass that overrides
            // the target (SingleStickVesselTransformer) is covered without knowing about drift.
            // _speedTrackingRate is deliberately left alone: a ramp boost that was mid-ramp
            // resumes on release instead of being silently consumed by the pinned value.
            if (_driftSpeedHeld)
            {
                speed = _heldDriftSpeed;
                return;
            }

            if (_speedTrackingRate > 0f)
            {
                speed = Mathf.MoveTowards(speed, target, _speedTrackingRate * Time.deltaTime);
                if (Mathf.Approximately(speed, target))
                    _speedTrackingRate = 0f;
            }
            else
            {
                speed = Mathf.Lerp(speed, target, LERP_AMOUNT * Time.deltaTime);
            }
        }

        protected virtual void MoveShip()
        {
            if (VesselStatus == null || InputStatus == null) return;

            // Smooth throttle speed calculation
            AdvanceSpeed(ComputeThrottleTarget());

            // Modifiers scale this frame's output speed only. Multiplying into the
            // persistent smoothed `speed` field compounds the modifier every frame,
            // saturating any sub-1 multiplier to a near-stop within a few frames -
            // which makes modifier strength untunable (a 0.5 floor and a 0.0 floor
            // both collapse to ~zero).
            float effectiveSpeed = speed * throttleMultiplier;

            // The manual-throttle channel is a throttle too, so a held drift silences it as well;
            // its contribution at the moment of capture is already folded into the held value.
            if (toggleManualThrottle && !_driftSpeedHeld)
                effectiveSpeed = Mathf.Lerp(0, effectiveSpeed, InputStatus.Throttle);

            VesselStatus.Speed = effectiveSpeed;

            // Drift course: blend between "go forward" and "drift course" based on analog intensity
            if ((VesselStatus.IsDrifting || _driftEaseOutPending) && _hasDriftBase)
            {
                float driftAmount = Mathf.Clamp01(_frameTriggerSum);

                // Compute the drifted course (slow convergence toward facing direction)
                Vector3 driftedCourse = DriftDamping > 0.001f
                    ? Vector3.Slerp(VesselStatus.Course, transform.forward,
                        DriftDamping * Time.deltaTime).normalized
                    : VesselStatus.Course;

                // Blend: at driftAmount 0, Course = forward (no drift feel);
                // at driftAmount 1, Course = fully drifted
                VesselStatus.Course = Vector3.Slerp(transform.forward, driftedCourse, driftAmount);
            }
            else
            {
                VesselStatus.Course = transform.forward;
            }

            transform.position += (effectiveSpeed * VesselStatus.Course + velocityShift) * Time.deltaTime;
        }

        // ----------------------------- Modifiers -----------------------------
        public void ModifyThrottle(float amount, float duration)
        {
            ThrottleModifiers.Add(new ShipThrottleModifier(amount, duration, 0));
        }

        private void ApplyThrottleModifiers()
        {
            float accumulatedThrottleModification = 1f;

            for (int i = ThrottleModifiers.Count - 1; i >= 0; i--)
            {
                var modifier = ThrottleModifiers[i];
                modifier.elapsedTime += Time.deltaTime;
                ThrottleModifiers[i] = modifier;

                if (modifier.elapsedTime >= modifier.duration)
                {
                    ThrottleModifiers.RemoveAt(i);
                    if (ThrottleModifiers.Count == 0)
                    {
                        VesselStatus.IsSlowed = false;
                        Vessel.RemoveSlowedShipTransformFromGameData();
                    }
                }
                else if (modifier.initialValue < 1f)
                {
                    accumulatedThrottleModification *= Mathf.Lerp(modifier.initialValue, 1f, modifier.elapsedTime / modifier.duration);
                    VesselStatus.IsSlowed = true;
                    Vessel.AddSlowedShipTransformToGameData();
                }
                else
                {
                    accumulatedThrottleModification += Mathf.Lerp(modifier.initialValue - 1f, 0f, modifier.elapsedTime / modifier.duration);
                }
            }

            accumulatedThrottleModification = Mathf.Clamp(accumulatedThrottleModification, 0f, speedModifierMax);

            if (accumulatedThrottleModification < 0.001f)
            {
                VesselStatus.IsSlowed = false;
                Vessel.RemoveSlowedShipTransformFromGameData();
            }

            throttleMultiplier = Mathf.Max(accumulatedThrottleModification, 0f);

            if (throttleMultiplier > 1f)
                VesselStatus.VesselAnimation?.FlareEngine();
            else
                VesselStatus.VesselAnimation?.StopFlareEngine();
        }

        /// <param name="translationRestricted">While true, every modifier still ages out, but
        /// only those flagged <see cref="ShipVelocityModifier.ignoresTranslationRestriction"/>
        /// contribute displacement.</param>
        private void ApplyVelocityModifiers(bool translationRestricted = false)
        {
            Vector3 accumulatedVelocity = Vector3.zero;

            for (int i = VelocityModifiers.Count - 1; i >= 0; i--)
            {
                var modifier = VelocityModifiers[i];
                modifier.elapsedTime += Time.deltaTime;
                VelocityModifiers[i] = modifier;

                if (modifier.elapsedTime >= modifier.duration)
                    VelocityModifiers.RemoveAt(i);
                else if (!translationRestricted || modifier.ignoresTranslationRestriction)
                    accumulatedVelocity += ((Mathf.Cos(modifier.elapsedTime * Mathf.PI / modifier.duration) / 2) + 1) * modifier.initialValue;
            }

            velocityShift = Vector3.ClampMagnitude(accumulatedVelocity, velocityModifierMax);

            var sqrMag = velocityShift.sqrMagnitude;

            if (sqrMag > 0.01f)
            {
                VesselStatus.VesselAnimation?.FlareBody(sqrMag / 4000);
                _bodyFlaring = true;
            }
            else if (_bodyFlaring)
            {
                // Edge-triggered on the way DOWN only: StopFlareBody writes through
                // `renderer.materials[0]`, which clones the material and allocates the array on
                // every call. Harmless-looking when this method only ran while flying, but it
                // now also runs for a stopped vessel, so pay it once per flare→rest transition.
                // Seeded true so the first pass still normalizes the material exactly as before.
                VesselStatus.VesselAnimation?.StopFlareBody();
                _bodyFlaring = false;
            }
        }

        public void TranslateShip(Vector3 nudgeVector)
        {
            transform.position += nudgeVector;
        }

        public void ModifyVelocity(Vector3 amount, float duration)
            => ModifyVelocity(amount, duration, false);

        /// <param name="ignoresTranslationRestriction">Opt this displacement out of the
        /// <c>IsTranslationRestricted</c> hold (see
        /// <see cref="ShipVelocityModifier.ignoresTranslationRestriction"/>). Reserved for
        /// dodges that must remain available in a stance that pins the vessel — do not set it
        /// to make an ordinary ability work while stopped.</param>
        public void ModifyVelocity(Vector3 amount, float duration, bool ignoresTranslationRestriction)
        {
            VelocityModifiers.Add(new ShipVelocityModifier(amount, duration, 0, ignoresTranslationRestriction));
        }
    }
}
