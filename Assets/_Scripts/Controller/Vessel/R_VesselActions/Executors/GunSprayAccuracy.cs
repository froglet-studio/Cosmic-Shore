using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The per-vessel accuracy state of the Sparrow's guns: how HOT they are, how wide the cone
    /// has opened along the profile's six-stage curve (grace → ramp → plateau → blow-out →
    /// plateau → collapse), and the haptic ramp that tells the pilot about it.
    ///
    /// It is a <see cref="ShipActionExecutorBase"/> because that is where per-vessel action
    /// state belongs — the action SOs are shared across every vessel of the class and must stay
    /// stateless (state on a shared SO is last-initializer-wins the moment two Sparrows exist).
    /// It executes no action of its own; both fire executors drive it, which is exactly what
    /// makes the bullets and the Turret Stance share ONE cone instead of authoring two.
    ///
    /// <b>Heat, not hold time.</b> The curve's time axis is HEAT: one second of fire adds one
    /// second of heat, and while the trigger is up heat drains at
    /// <see cref="GunSpreadProfile.CoolingRateMultiplier"/> times that rate (5x as shipped, so a
    /// fully-hot gun — 30 s of heat — is cold after 6 s off). The
    /// trigger coming up does NOT reset accuracy — a release buys accuracy back only in
    /// proportion to how long the pilot lets go, so spraying and tapping are one continuous
    /// economy rather than two modes with a free reset between them. Heat is capped at the
    /// curve's full-spread point, so a fully-hot gun is never more than
    /// <c>SecondsToFullSpread / cooling</c> from cold.
    ///
    /// Integrated in <see cref="Update"/> off <see cref="Time.deltaTime"/>, so a paused game
    /// neither heats nor cools the gun. A stance flip (which stops one fire action and starts the
    /// other synchronously) lands inside one frame and so costs no heat either way.
    /// </summary>
    public sealed class GunSprayAccuracy : ShipActionExecutorBase
    {
        [Tooltip("The asset that authors this vessel's spread profile. Seeds the curve at " +
                 "Initialize so the heat gauge can draw its phase marks before the first shot; " +
                 "each trigger pull still refreshes the profile from the action that fired.")]
        [SerializeField] FullAutoActionSO profileSource;

        IVesselStatus _status;
        GunSpreadProfile _profile;

        bool _holding;
        float _heatSeconds;
        float _nextHapticTime;

        /// <summary>
        /// Monotonic across the whole session — deliberately NOT reset per hold. Resetting it
        /// would make every trigger pull replay the same sequence of deflections, which is a
        /// learnable pattern rather than the stochastic cone the design asks for.
        /// </summary>
        uint _shotSerial;

        /// <summary>True while the trigger is down (a mid-hold stance flip does not clear it).</summary>
        public bool IsHolding => _holding;

        /// <summary>The profile the heat is measured against. Null until seeded or first fired.</summary>
        public GunSpreadProfile Profile => _profile;

        /// <summary>The cone's current half-angle in degrees. 0 = perfectly accurate.</summary>
        public float HalfAngleDegrees { get; private set; }

        /// <summary>
        /// Fraction of the authored range a round fired NOW keeps, 1 at a cold gun falling
        /// linearly with the cone to <see cref="GunSpreadProfile.RangeAtFullSpread"/> at the final
        /// cap (<see cref="GunSpreadMath.RangeFactor"/>). Both fire executors multiply their muzzle
        /// speed by it, so the bullets and the Turret Stance's prisms lose range together.
        /// </summary>
        public float RangeFactor { get; private set; } = 1f;

        /// <summary>Current heat in SECONDS of the curve's time axis, 0..SecondsToFullSpread.</summary>
        public float HeatSeconds => _heatSeconds;

        /// <summary>
        /// Heat as a fraction of the heat ceiling (the full-spread point), 0..1. What the HUD
        /// gauge draws; <see cref="CollectPhaseJoins01"/> gives the marks on the same axis.
        /// </summary>
        public float Heat01
        {
            get
            {
                float full = _profile?.SecondsToFullSpread ?? 0f;
                return full > 0f ? Mathf.Clamp01(_heatSeconds / full) : 0f;
            }
        }

        /// <summary>
        /// How far the cone has opened toward its SUSTAINABLE cap, 0..1 — deliberately not
        /// toward the blow-out or collapse cap. It drives the haptic ramp, and both of that ramp's
        /// channels are already at their ceiling when the plateau is reached (strength 1.0, and a
        /// pulse interval already inside the spray clip's own length), so there is no headroom
        /// left to spend beyond it. Pinned at 1 past the first cap, which is itself the signal:
        /// the buzz stops climbing because the gun already has.
        /// </summary>
        public float Saturation01 { get; private set; }

        static readonly List<float> s_joins = new();

        /// <summary>
        /// Every phase transition of the curve (onset ending, each ramp capping out, each plateau
        /// expiring) as a fraction of the heat ceiling, ascending, EXCLUDING the final 1.0 — i.e.
        /// the interior marks a heat gauge draws. Cleared and filled; empty when no profile or no
        /// spread is authored.
        /// </summary>
        public void CollectPhaseJoins01(List<float> into)
        {
            into.Clear();
            if (_profile == null) return;

            var stages = _profile.Stages;
            float full = stages.SecondsToFullSpread;
            if (full <= 0f) return;

            stages.CollectPhaseJoins(s_joins);
            for (int i = 0; i < s_joins.Count; i++)
            {
                float f = s_joins[i] / full;
                if (f < 0.9999f) into.Add(f);
            }
        }

        public override void Initialize(IVesselStatus vesselStatus)
        {
            _status = vesselStatus;
            if (_profile == null && profileSource) _profile = profileSource.Spread;
            ResetHeat();
        }

        /// <summary>
        /// The trigger went down — or a fire loop restarted mid-hold. Idempotent: a second call
        /// while already holding only refreshes the profile, so the mode-switch hand-off between
        /// bullets and turret prisms carries the heat across (it would anyway — heat is never
        /// reset by the trigger).
        /// </summary>
        public void BeginHold(GunSpreadProfile profile)
        {
            _profile = profile;
            if (_holding) return;

            _holding = true;
            _nextHapticTime = 0f;   // first pulse lands on the first frame of fire
        }

        /// <summary>The trigger came up (or the loop was stopped). The gun starts COOLING; the
        /// cone is not reset.</summary>
        public void ReleaseHold() => _holding = false;

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
            if (_profile == null) return;

            float full = _profile.SecondsToFullSpread;
            float dt = Time.deltaTime;

            _heatSeconds = _holding
                ? Mathf.Min(full, _heatSeconds + dt)
                : Mathf.Max(0f, _heatSeconds - dt * _profile.CoolingRateMultiplier);

            var stages = _profile.Stages;
            HalfAngleDegrees = GunSpreadMath.HalfAngleDegrees(_heatSeconds, stages);
            RangeFactor = GunSpreadMath.RangeFactor(
                HalfAngleDegrees, stages.FinalMaxHalfAngleDegrees, _profile.RangeAtFullSpread);

            Saturation01 = _profile.MaxHalfAngleDegrees > 0f
                ? Mathf.Clamp01(HalfAngleDegrees / _profile.MaxHalfAngleDegrees)
                : 0f;

            if (_holding) DriveHaptics();
        }

        // A vessel being disabled (pooled, swapped, torn down) comes back cold with its trigger
        // up — its trigger is definitionally up, and a swapped-in hull has fired nothing.
        void OnDisable() => ResetHeat();

        /// <summary>
        /// The rising buzz. Both the STRENGTH and the CADENCE climb with the cone, which is what
        /// makes it read as a gun winding up rather than a constant hum.
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

        void ResetHeat()
        {
            _holding = false;
            _heatSeconds = 0f;
            HalfAngleDegrees = 0f;
            RangeFactor = 1f;
            Saturation01 = 0f;
        }
    }
}
