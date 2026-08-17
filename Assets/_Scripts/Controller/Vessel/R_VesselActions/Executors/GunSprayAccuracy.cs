using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The per-vessel accuracy state of a held trigger: how long the gun has been spraying,
    /// how wide its cone has opened, and the haptic ramp that tells the pilot about it.
    ///
    /// It is a <see cref="ShipActionExecutorBase"/> because that is where per-vessel action
    /// state belongs — the action SOs are shared across every vessel of the class and must stay
    /// stateless (state on a shared SO is last-initializer-wins the moment two Sparrows exist).
    /// It executes no action of its own; both fire executors drive it, which is exactly what
    /// makes the bullets and the Turret Stance share ONE cone instead of authoring two.
    ///
    /// <b>Reset semantics.</b> Releasing the trigger resets accuracy completely, so a
    /// release-and-re-pull always buys a fresh onset window and short bursts stay surgical.
    /// The reset is deferred by one frame (<see cref="LateUpdate"/>) for one specific reason:
    /// toggling the Turret Stance mid-hold makes <c>SparrowModeSwitchingFireSO</c> stop one fire
    /// action and start the other **synchronously**, and without the deferral that internal
    /// hand-off would read as a trigger release and hand the pilot a free accuracy reset. The
    /// fire loops run at <c>PreLateUpdate</c>, so a real release still lands before the next
    /// volley — the deferral is invisible in play.
    /// </summary>
    public sealed class GunSprayAccuracy : ShipActionExecutorBase
    {
        IVesselStatus _status;
        GunSpreadProfile _profile;

        bool _holding;
        bool _releasePending;
        float _holdStartTime;
        float _nextHapticTime;

        /// <summary>
        /// Monotonic across the whole session — deliberately NOT reset per hold. Resetting it
        /// would make every trigger pull replay the same sequence of deflections, which is a
        /// learnable pattern rather than the stochastic cone the design asks for.
        /// </summary>
        uint _shotSerial;

        /// <summary>True while the trigger is down (a mid-hold stance flip does not clear it).</summary>
        public bool IsHolding => _holding;

        /// <summary>The cone's current half-angle in degrees. 0 = perfectly accurate.</summary>
        public float HalfAngleDegrees { get; private set; }

        /// <summary>How far the cone has opened toward its cap, 0..1. Drives the haptic ramp.</summary>
        public float Saturation01 { get; private set; }

        public override void Initialize(IVesselStatus vesselStatus)
        {
            _status = vesselStatus;
            ResetHold();
        }

        /// <summary>
        /// The trigger went down — or a fire loop restarted mid-hold. Idempotent by design: a
        /// second call while already holding only refreshes the profile, so the mode-switch
        /// hand-off between bullets and turret prisms carries the accumulated spread across.
        /// </summary>
        public void BeginHold(GunSpreadProfile profile)
        {
            _profile = profile;
            _releasePending = false;

            if (_holding) return;

            _holding = true;
            _holdStartTime = Time.time;
            _nextHapticTime = 0f;   // first pulse lands on the first frame of fire
            HalfAngleDegrees = 0f;
            Saturation01 = 0f;
        }

        /// <summary>
        /// The trigger came up (or the loop was stopped). Arms the reset; <see cref="LateUpdate"/>
        /// applies it unless a <see cref="BeginHold"/> arrives first in the same frame.
        /// </summary>
        public void ReleaseHold()
        {
            if (_holding) _releasePending = true;
        }

        /// <summary>
        /// One round's direction: the muzzle's aim deflected somewhere inside the current cone.
        /// Consumes one step of the deterministic shot stream, so consecutive rounds — including
        /// two muzzles firing in the same frame — scatter independently.
        /// </summary>
        public Vector3 PerturbAim(Vector3 forward)
        {
            unchecked { _shotSerial++; }

            if (HalfAngleDegrees <= 0f)
                return forward.sqrMagnitude > 1e-12f ? forward.normalized : forward;

            return GunSpreadMath.Perturb(
                forward, HalfAngleDegrees, _profile?.DistributionBias ?? 0.5f, _shotSerial);
        }

        void Update()
        {
            if (!_holding || _profile == null) return;

            HalfAngleDegrees = GunSpreadMath.HalfAngleDegrees(
                Time.time - _holdStartTime,
                _profile.OnsetSeconds,
                _profile.GrowthDegreesPerSecond,
                _profile.MaxHalfAngleDegrees);

            Saturation01 = _profile.MaxHalfAngleDegrees > 0f
                ? Mathf.Clamp01(HalfAngleDegrees / _profile.MaxHalfAngleDegrees)
                : 0f;

            DriveHaptics();
        }

        void LateUpdate()
        {
            if (_releasePending) ResetHold();
        }

        // A vessel being disabled (pooled, swapped, torn down) must not resume a stale hold when
        // it comes back — its trigger is definitionally up.
        void OnDisable() => ResetHold();

        /// <summary>
        /// The rising buzz. Both the STRENGTH and the CADENCE climb with the cone, which is what
        /// makes it read as a gun winding up rather than a constant hum — and the escalation is
        /// the only cue the pilot gets that their accuracy is going, since the spread itself is
        /// only visible once rounds start missing.
        ///
        /// Local human pilot only, exactly like the other feels: remote players, AI dogfighters
        /// and the Menu_Main autopilot all fire, and none of them may buzz this device.
        /// </summary>
        void DriveHaptics()
        {
            if (!_profile.Enabled) return;
            if (_status?.Player == null || !_status.IsLocalUser || _status.AutoPilotEnabled) return;

            float now = Time.unscaledTime;
            if (now < _nextHapticTime) return;

            _nextHapticTime = now + Mathf.Lerp(
                _profile.HapticIntervalAtRest, _profile.HapticIntervalAtMaxSpread, Saturation01);

            HapticController.PlaySpray(Mathf.Lerp(_profile.HapticFloor01, 1f, Saturation01));
        }

        void ResetHold()
        {
            _holding = false;
            _releasePending = false;
            HalfAngleDegrees = 0f;
            Saturation01 = 0f;
        }
    }
}
