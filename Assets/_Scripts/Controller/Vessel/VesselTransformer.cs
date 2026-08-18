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
/// <summary>
/// The fleet's movement base. It carries TWO flight models, selected per vessel by
/// <c>vectorFlightModel</c>:
///
/// <b>SCALAR (default, historical).</b> A single smoothed <see cref="speed"/> eased toward
/// <see cref="ComputeThrottleTarget"/>, integrated along <c>VesselStatus.Course</c>.
///
/// <b>VECTOR (opt-in).</b> A world-space <see cref="_velocity"/> is integrated directly: thrust
/// is applied along the <b>NOSE</b>, momentum carries you along the old line, and
/// <see cref="Grip"/> rotates the two back together. Course and Speed are then DERIVED from that
/// vector rather than being the primitives.
///
/// <b>Why the vector model exists.</b> The scalar model's throttle is a number with no direction,
/// so it can only push along Course. Outside a drift Course == forward and that is fine. Inside
/// one they are different vectors, and pushing along Course means squeezing the throttle mid-drift
/// digs you DEEPER into the slide — the drift reads as ice rather than as driving. No amount of
/// tuning fixes that, because the thrust direction is wrong; it needs the second vector.
///
/// <b>THE IDENTITY (this is why the flag is cheap and needs no fleet retune).</b> Outside a drift
/// the two models are provably the same computation. Grip forces <c>v = forward·s</c>, so
/// <c>dot(v, forward) == |v| == speed</c>; the nose step
/// <c>v += forward·(step(speed, target) − speed)</c> leaves <c>|v| = step(speed, target)</c>,
/// which is exactly what <see cref="AdvanceSpeed"/> writes; <c>Course = v/|v| = forward</c>, which
/// is exactly what the scalar branch writes; and <c>position += |v|·Course·dt</c> is the same
/// integration. Both paths call the same <see cref="StepTowardTarget"/>, so this is one shared
/// function, not two implementations that happen to agree. A vessel with the flag OFF is
/// bit-identical to before the flag existed, and a vessel with it ON differs only inside the
/// drift window.
/// </summary>
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

    #region Flight model
    /// <summary>What the throttle is allowed to do while the vessel is drifting. Only consulted
    /// by the VECTOR flight model — on the scalar path the throttle is always live.</summary>
    public enum DriftThrottlePolicy
    {
        /// <summary>Thrust keeps acting, along the NOSE. Aiming out of a slide and squeezing is
        /// how you recover — the racer's answer.</summary>
        Live = 0,

        /// <summary>No acceleration for the drift's duration. With Grip 0 this freezes the
        /// velocity vector outright — direction AND magnitude — so the drift is a hard
        /// momentum lock rather than a steering option. The Dolphin's authored mechanic.</summary>
        Locked = 1,
    }

    [Header("Flight model")]
    [Tooltip("Integrate a world-space VELOCITY VECTOR instead of a scalar speed along Course.\n\n" +
             "OFF (default) is the fleet's historical scalar model and is untouched.\n\n" +
             "ON fixes the drift defect: the scalar model applies thrust along COURSE, so " +
             "squeezing the throttle mid-drift digs you DEEPER into the slide instead of pulling " +
             "you out — the drift reads as ice rather than as driving. Under the vector model " +
             "thrust always acts along the NOSE while momentum carries you down the old line.\n\n" +
             "OUTSIDE A DRIFT THE TWO MODELS ARE PROVABLY IDENTICAL (see the class docs), so " +
             "this flag changes behaviour only inside the drift window and needs no retune.")]
    [SerializeField] bool vectorFlightModel = false;

    [Tooltip("VECTOR MODEL ONLY. Ceiling on |velocity| while drifting, as a multiple of the " +
             "current throttle target. Vector addition lets momentum + nose-thrust exceed the " +
             "target during a drift — a real speed payoff for a clean line, which the scalar " +
             "model cannot produce — and this bounds it so drifting cannot become the dominant " +
             "way to go fast. 1 = no overshoot at all. Ignored outside a drift, which is what " +
             "keeps the no-drift identity exact.")]
    [SerializeField, Min(1f)] float driftOvershootCeiling = 1.25f;

    [Tooltip("VECTOR MODEL ONLY. Whether the throttle keeps acting during a drift (see the " +
             "enum's own docs).")]
    [SerializeField] DriftThrottlePolicy driftThrottlePolicy = DriftThrottlePolicy.Live;
    #endregion

    /// <summary>
    /// How fast momentum rotates back onto the nose while drifting — the tyres' bite. Written
    /// every frame by <see cref="ApplyAnalogDrift"/> from the active drift tier's authored
    /// damping (0 = no convergence at all, a pure frozen slide), and zeroed by
    /// <see cref="RestoreDriftBase"/>. Was named <c>DriftDamping</c>; the new name is what it has
    /// always meant. A `[HideInInspector] public` runtime mirror — prefab-serialized values are
    /// stale garbage, exactly like <see cref="ThrottleScaler"/>.
    /// </summary>
    [FormerlySerializedAs("DriftDamping")]
    [HideInInspector] public float Grip = 0f;

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

            // Vector flight model: drop the momentum vector and re-seed on the next frame from
            // whatever `speed` is by then, so an inherited-speed swap (SetInitialSpeed after a
            // reset) still works and a respawn does not carry the previous life's heading.
            _velocity = Vector3.zero;
            _lastPublishedSpeed = 0f;
            _lastPublishedCourse = Vector3.zero;
            _vectorSeeded = false;

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

            // A pose write is a teleport, so momentum must follow the new facing rather than the
            // old one. Outside a drift grip would snap it back within a frame anyway; mid-drift
            // (a Wanderway return, a swap while sliding) it would otherwise keep flying the old
            // heading out of a hull that is now pointing somewhere else.
            if (_vectorSeeded) SetCourseVelocity(pose.rotation * Vector3.forward);
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
            Grip = 0f;
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
            Grip = effectiveDamp;
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

        /// <summary>
        /// One frame of tracking from <paramref name="current"/> toward <paramref name="target"/> —
        /// constant-rate while a tracking rate is set (see <see cref="SetSpeedTrackingRate"/>),
        /// exponential lerp otherwise. Returns the new value rather than writing anything, because
        /// BOTH flight models step through here: the scalar path applies it to
        /// <see cref="speed"/>, the vector path to the velocity's nose component. That shared call
        /// is what makes the no-drift identity an identity instead of a coincidence.
        ///
        /// The tracking rate is cleared ONLY on landing, and left completely alone otherwise — the
        /// Rhino's ramp boost latches it across frames and a mid-ramp boost must resume, so this
        /// must never consume it speculatively.
        /// </summary>
        protected float StepTowardTarget(float current, float target, float dt)
        {
            // A held drift pins the smoothed cruise speed at the value the vessel carried in:
            // the caller still computes a throttle target, it simply never reaches `speed` -
            // which IS what "the throttle is disabled during the drift" means mechanically.
            // This lives here rather than in ComputeThrottleTarget because AdvanceSpeed is the
            // one path every transformer's MoveShip runs through, so a subclass that overrides
            // the target (SingleStickVesselTransformer) is covered without knowing about drift.
            // _speedTrackingRate is deliberately left alone: a ramp boost that was mid-ramp
            // resumes on release instead of being silently consumed by the pinned value.
            //
            // MERGE NOTE: upstream authored this against the older void `AdvanceSpeed`, which
            // assigned `speed` and returned. This method is now the shared pure step BOTH flight
            // models run through, so the pinned value is RETURNED rather than written — writing
            // the field here would have left the vector model integrating a velocity whose
            // magnitude nothing had agreed to. The vector model's equivalent of this hold is
            // `DriftThrottlePolicy.Locked`, which stops nose acceleration for the drift's
            // duration; a vessel should author one or the other, not both.
            if (_driftSpeedHeld)
                return _heldDriftSpeed;

            if (_speedTrackingRate > 0f)
            {
                float next = Mathf.MoveTowards(current, target, _speedTrackingRate * dt);
                if (Mathf.Approximately(next, target))
                    _speedTrackingRate = 0f;
                return next;
            }
            return Mathf.Lerp(current, target, LERP_AMOUNT * dt);
        }

        /// <summary>Advance the smoothed cruise speed one frame toward
        /// <paramref name="target"/>. Scalar path only.</summary>
        protected void AdvanceSpeed(float target)
            => speed = StepTowardTarget(speed, target, Time.deltaTime);

        // ----------------------------- Vector flight model -----------------------------

        /// <summary>World-space momentum. Authoritative only while <c>vectorFlightModel</c> is on;
        /// <see cref="speed"/> and <c>VesselStatus.Course</c> are then derived from it every
        /// frame (and <see cref="speed"/> is still published, because the fleet's rotation math
        /// reads it as <c>speed * RotationThrottleScaler</c>).</summary>
        Vector3 _velocity;
        float _lastPublishedSpeed;
        Vector3 _lastPublishedCourse;
        bool _vectorSeeded;

        /// <summary>How strongly this frame's travel is a drift: 0 outside the drift window,
        /// rising to 1 at full analog trigger. Shared by both models so the drift blend and the
        /// overshoot ceiling can never disagree about whether a drift is happening.</summary>
        protected float DriftBlend01()
            => VesselStatus != null && (VesselStatus.IsDrifting || _driftEaseOutPending) && _hasDriftBase
                ? Mathf.Clamp01(_frameTriggerSum)
                : 0f;

        /// <summary>
        /// Speed gained along the NOSE this frame (world units, already multiplied by dt). This is
        /// the ONLY thing a vessel's flight policy has to supply — everything else about the
        /// vector model (grip, publishing, the modifier channels, integration, external-write
        /// re-seeding) is owned here.
        ///
        /// Base policy: track <see cref="ComputeThrottleTarget"/> proportionally, exactly as the
        /// scalar path does, but measured along the nose instead of along the travel direction.
        /// <see cref="DriftThrottlePolicy.Locked"/> returns 0 for the drift's duration.
        /// </summary>
        protected virtual float ComputeNoseAcceleration(float dt)
        {
            if (driftThrottlePolicy == DriftThrottlePolicy.Locked && DriftBlend01() > 0f)
                return 0f;

            float along = Vector3.Dot(_velocity, transform.forward);
            return StepTowardTarget(along, ComputeThrottleTarget(), dt) - along;
        }

        /// <summary>
        /// Magnitude policy, applied after grip and thrust. Base implementation is the drift
        /// overshoot ceiling and nothing else — deliberately the IDENTITY outside a drift, which
        /// is what keeps the no-drift equivalence exact. Vessels with a real speed model of their
        /// own (the Scarab's release-only drag + hard ceiling) replace it wholesale.
        ///
        /// <b>THE CEILING BOUNDS GAIN — IT MUST NEVER BRAKE.</b> Clamping to
        /// <c>ComputeThrottleTarget() × ceiling</c> outright looks equivalent and is not: a vessel
        /// that ENTERED the drift fast gets slammed down to its current cruise target on the very
        /// first drift frame. That shipped for one round and was exactly the two symptoms reported
        /// on the Dolphin — "loses a ton of speed when the drift is initiated" (its boosted 357
        /// u/s hitting a 55 u/s ceiling, because `ChargeBoostAction.BeginCharge` clears the boost
        /// on drift entry so the target collapses to the unboosted cruise) and "controls its speed
        /// during the drift" (the ceiling tracking `XDiff`, so the scissor throttle moved the
        /// clamp). Taking <c>speedBeforeThrust</c> as a floor makes this bound this frame's
        /// INCREASE only; a fast entry then decays toward the target through
        /// <see cref="ComputeNoseAcceleration"/>, which is where deceleration belongs.
        /// </summary>
        /// <param name="speedBeforeThrust">|velocity| at the top of the frame (grip preserves
        /// magnitude, so this is also the post-grip magnitude).</param>
        protected virtual float ShapeSpeed(float speedNow, float speedBeforeThrust, float dt)
        {
            if (DriftBlend01() <= 0f) return speedNow;
            float ceiling = Mathf.Max(speedBeforeThrust, ComputeThrottleTarget() * driftOvershootCeiling);
            return Mathf.Min(speedNow, ceiling);
        }

        /// <summary>Fraction of the remaining nose-ward angle that grip closes this frame.
        /// Frame-rate independent (<c>1 − e^(−k·dt)</c>) rather than the scalar path's raw
        /// <c>k·dt</c>: at 60 fps the two differ by ~0.4% at the Squirrel's authored grip, so this
        /// does not perturb the tuning, but it stops a frame-rate drop from loosening the back
        /// end. Applies only inside the drift window, so it cannot touch the identity claim.</summary>
        float GripFraction(float dt) => Grip > 0.0001f ? 1f - Mathf.Exp(-Grip * dt) : 0f;

        /// <summary>
        /// Re-aim the momentum vector along <paramref name="direction"/>, preserving its
        /// magnitude. The vector model's counterpart to <see cref="SetInitialSpeed"/>: anything
        /// that legitimately dictates a vessel's TRAVEL direction from outside calls this instead
        /// of writing <c>VesselStatus.Course</c>. (Writing Course still works — see
        /// <see cref="SyncExternalWrites"/> — this is just the explicit door.)
        /// </summary>
        public void SetCourseVelocity(Vector3 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) return;
            Vector3 unit = direction.normalized;
            _velocity = unit * _velocity.magnitude;
            _lastPublishedCourse = unit;
            if (VesselStatus != null) VesselStatus.Course = unit;
        }

        void SeedVectorState()
        {
            if (_vectorSeeded) return;
            _velocity = transform.forward * speed;
            _lastPublishedSpeed = speed;
            _lastPublishedCourse = transform.forward;
            _vectorSeeded = true;
        }

        /// <summary>
        /// Adopt writes that came from OUTSIDE this transformer since our last publish. Two of
        /// them exist and both are load-bearing:
        ///
        /// <b>speed</b> — the menu vessel swap's <see cref="SetInitialSpeed"/> and spawn's
        /// <see cref="ResetTransformer"/> write the scalar directly. Without this a swap would
        /// silently drop the new hull to a dead stop.
        ///
        /// <b>Course</b> — <c>AIPilot</c> writes <c>VesselStatus.Course = desiredDirection</c> at
        /// drift entry, and that write IS the AI's drift: the course locks onto the objective
        /// while the nose swings away, which is how a drifting AI lays trail, skims and fires
        /// along an axis that is not its heading. The scalar path honours it for free by reading
        /// Course back and slerping FROM it; a vector model that derived Course purely from its
        /// own state would overwrite the AI every frame and the manoeuvre would silently stop
        /// working. Detecting it here keeps AIPilot unchanged and keeps the two models' AI
        /// behaviour matched.
        /// </summary>
        void SyncExternalWrites()
        {
            if (!Mathf.Approximately(speed, _lastPublishedSpeed))
            {
                Vector3 dir = _velocity.sqrMagnitude > 1e-6f ? _velocity.normalized : transform.forward;
                _velocity = dir * speed;
                _lastPublishedSpeed = speed;
            }

            Vector3 course = VesselStatus.Course;
            if (course.sqrMagnitude <= 1e-6f) return;
            Vector3 unit = course.normalized;
            if (Vector3.Dot(unit, _lastPublishedCourse) < 0.99999f)
            {
                _velocity = unit * _velocity.magnitude;
                _lastPublishedCourse = unit;
            }
        }

        protected virtual void MoveShip()
        {
            if (VesselStatus == null || InputStatus == null) return;

            if (vectorFlightModel) MoveShipVector();
            else MoveShipScalar();
        }

        void MoveShipVector()
        {
            float dt = Time.deltaTime;
            SeedVectorState();
            SyncExternalWrites();

            // 1) GRIP — momentum rotates back onto the nose. Outside a drift this snaps outright
            //    (convergence 1); inside one it closes only as fast as the active drift tier's
            //    authored grip allows (0 = never, a pure frozen slide).
            //
            //    GRIP RUNS BEFORE THRUST, AND THE ORDER IS LOAD-BEARING. Thrust-then-grip leaves
            //    |v| = sqrt(s² + d² + 2sd·cosθ) for a frame in which the nose turned by θ, which
            //    is *not* the scalar model's s + d — the no-drift equivalence would then hold only
            //    while flying dead straight, and drift by a second-order term whenever the vessel
            //    turned. Resolving grip first makes v exactly forward·s before thrust is measured,
            //    so the identity is unconditional instead of approximate. It is also the more
            //    honest physics: this frame's thrust should not itself be rotated by this frame's
            //    grip.
            float speedNow = _velocity.magnitude;
            if (speedNow > 1e-4f)
            {
                float driftAmount = DriftBlend01();
                float convergence = driftAmount > 0f
                    ? Mathf.Clamp01(Mathf.Lerp(1f, GripFraction(dt), driftAmount))
                    : 1f;
                _velocity = Vector3.Slerp(_velocity / speedNow, transform.forward, convergence) * speedNow;
            }
            else
            {
                _velocity = Vector3.zero;
            }

            // 2) THRUST ALONG THE NOSE — never along the current course. This one line is the
            //    whole point of the model: mid-drift the engine pushes where you POINT, so aiming
            //    out of a slide and squeezing is how you recover.
            _velocity += transform.forward * ComputeNoseAcceleration(dt);

            // 3) Magnitude policy (drift overshoot ceiling; the Scarab replaces this entirely).
            //    speedNow is still the pre-thrust magnitude here — grip preserves magnitude — and
            //    the ceiling needs it as a floor so it can only bound GAIN, never brake.
            speedNow = ShapeSpeed(_velocity.magnitude, speedNow, dt);
            _velocity = speedNow > 1e-4f ? _velocity.normalized * speedNow : Vector3.zero;

            // `speed` stays the fleet's API — the rotation scalers read it — so it tracks the
            // vector's magnitude exactly.
            speed = speedNow;
            _lastPublishedSpeed = speedNow;

            // Modifier channels are UNCHANGED by the flight model and must stay live during a
            // drift: throttleMultiplier is how a danger prism slows you and velocityShift is how
            // knockback moves you. Freezing either while drifting would make a drifting vessel
            // immune to danger prisms — a locked-design violation wearing a feel change's costume.
            float effectiveSpeed = speedNow * throttleMultiplier;

            if (toggleManualThrottle)
                effectiveSpeed = Mathf.Lerp(0, effectiveSpeed, InputStatus.Throttle);

            VesselStatus.Speed = effectiveSpeed;
            VesselStatus.Course = speedNow > 1e-4f ? _velocity / speedNow : transform.forward;
            _lastPublishedCourse = VesselStatus.Course;

            transform.position += (effectiveSpeed * VesselStatus.Course + velocityShift) * dt;
        }

        void MoveShipScalar()
        {
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
                Vector3 driftedCourse = Grip > 0.001f
                    ? Vector3.Slerp(VesselStatus.Course, transform.forward,
                        Grip * Time.deltaTime).normalized
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
