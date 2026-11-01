using CosmicShore.Core;
using CosmicShore.Data;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runs a <see cref="RampBoostActionSO"/>: while the pilot is flying straight the boosted
    /// throttle target is set and the transformer is put into constant-rate speed tracking, so
    /// the vessel accelerates linearly toward top speed instead of the default exponential lerp.
    ///
    /// <para><b>It does not stop at the gesture's threshold.</b> Once armed, this executor reads
    /// the live <see cref="StraightLineGesture"/> deviation every frame and GRADES its own output
    /// by it (<see cref="RampBoostActionSO.MultiplierFor"/>): full power across the plateau, then
    /// a linear fall to plain cruise at the SO's grace band. The input strategy's release at the
    /// engage threshold is therefore deliberately NOT a disengagement — it is the top of the
    /// slope, and the executor owns the rest of the way down. See RHINO_RAMP_BOOST.md.</para>
    ///
    /// <para><b>Every peer grades, from the same sticks.</b> Press and release round-trip through
    /// <c>R_VesselActionHandler</c>, so this executor is live on every peer, and the sticks it
    /// grades by are <c>InputStatus</c>'s owner-write NetworkVariables — the owner's fingers, or an
    /// AI's writes on the server, read identically everywhere. Until 2026-10-16 only the owner
    /// graded and a non-owning peer kept the full multiplier "while armed". But a graded ramp is
    /// never disarmed by an input event (the release at the engage threshold is the top of the
    /// slope, <see cref="ReleaseGesture"/>), only by leaving the grace band in <see cref="Tick"/> —
    /// so every replica held <c>IsBoosting</c> and the ×16.8 multiplier for the rest of the turn
    /// after the first ramp, and every per-peer reader of either (immunity windows, telemetry, the
    /// roll controller) read a boost that had ended seconds ago.</para>
    /// </summary>
    public sealed class RampBoostActionExecutor : ShipActionExecutorBase, ITopSpeedSource
    {
        [Inject] AudioSystem _audioSystem;

        [SerializeField] public Obvious.Soap.ScriptableEventNoParam OnMiniGameTurnEnd;

        [Tooltip("The ramp asset this hull binds - read only for TopSpeed before the ramp has been " +
                 "armed once. Optional: once a ramp has run, the asset that armed it is used.")]
        [SerializeField] RampBoostActionSO topSpeedAsset;

        IVesselStatus _status;
        RampBoostActionSO _activeSO;
        RampBoostActionSO _lastSO;   // the asset that last armed us - the top-speed yardstick between ramps
        float _restingMultiplier = 1f;
        GameplaySFXCategory _engagedSFX;   // the held engage sound Begin started; End stops it

        public bool IsEngaged => _activeSO != null;

        /// <summary>
        /// The hull's ramp TOP SPEED, u/s: its AUTHORED unboosted cruise (<c>DefaultMinimumSpeed +
        /// DefaultThrottleScaler</c> - not the runtime mirrors, which read 0 on a replica whose
        /// transformer never reset) times the ramp's <see cref="RampBoostActionSO.MaxBoostMultiplier"/>:
        /// (0 + 50) × 16.8 = 840 on the Rhino. 0 with no ramp asset or no transformer. The yardstick
        /// the Time level-5 ward measures "fast" against.
        /// <para>Before the first ramp it falls back to the asset serialized here, so a ward reads a
        /// real ceiling from the first frame rather than "unknown".</para>
        /// </summary>
        public float TopSpeed
        {
            get
            {
                var so = _activeSO ? _activeSO : _lastSO ? _lastSO : topSpeedAsset;
                var transformer = _status?.VesselTransformer;
                if (!so || !transformer) return 0f;
                float cruise = Mathf.Max(0f, transformer.DefaultMinimumSpeed) + Mathf.Max(0f, transformer.DefaultThrottleScaler);
                return cruise * so.MaxBoostMultiplier;
            }
        }

        /// <summary>How much of the ramp the pilot is holding this frame, 0..1. HUD-facing.</summary>
        public float Straightness01 { get; private set; }

        void OnEnable()
        {
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            End();
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
        }

        void OnTurnEndOfMiniGame() => End();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
        }

        public void Begin(RampBoostActionSO so, IVesselStatus status)
        {
            if (status == null || so == null) return;

            // Re-arming while already engaged is ORDINARY here, not an error: the strategy
            // re-raises the press every time deviation dips back under the threshold, which on a
            // graded ramp happens on the exit of every corner. Refresh and keep the ramp — a
            // full End()/Begin() would restore the resting multiplier for one frame and snapshot
            // a graded value as the new resting one.
            if (_activeSO != null)
            {
                _activeSO = so;
                _status = status;
                return;
            }

            _status = status;
            _activeSO = so;
            _lastSO = so;
            _restingMultiplier = status.BoostMultiplier;
            status.IsBoosting = true;
            status.IsStationary = false;

            Straightness01 = 1f;
            status.BoostMultiplier = so.MaxBoostMultiplier;
            var transformer = status.VesselTransformer;
            if (transformer != null)
                transformer.SetSpeedTrackingRate(ClimbRate(so));

            // Held for the whole ramp: the default EngageSFX (Boost Activate) is a looping bed
            // that the one-shot path refuses. End() stops it; a one-shot category still just plays.
            _engagedSFX = so.EngageSFX;
            HeldVesselSfx.Begin(_audioSystem, _engagedSFX, this, status);
        }

        /// <summary>
        /// The gesture lapsed at the engage threshold. On a GRADED ramp that is the top of the
        /// slope and not a disengagement — <see cref="Tick"/> owns the rest, and ends the ramp
        /// when the pilot leaves the grace band. An asset authored with no band (the legacy
        /// binary latch) still ends here, so the old configuration behaves exactly as it did.
        /// </summary>
        public void ReleaseGesture()
        {
            if (_activeSO == null) return;
            if (_activeSO.StraightnessGraceBand <= StraightLineGesture.EngageThreshold) End();
        }

        public void End()
        {
            if (_activeSO == null) return;

            var so = _activeSO;
            _activeSO = null;
            Straightness01 = 0f;
            HeldVesselSfx.End(_audioSystem, _engagedSFX, this);

            if (_status == null) return;

            _status.BoostMultiplier = _restingMultiplier;
            _status.IsBoosting = false;

            // Constant-rate coast back to the input-driven throttle speed; the transformer
            // reverts to its normal smoothing once the speed lands on the target.
            var transformer = _status.VesselTransformer;
            if (transformer != null)
                transformer.SetSpeedTrackingRate(so.ReturnPerSecond);
        }

        void Update()
        {
            if (_activeSO == null) return;
            Tick();
        }

        void Tick()
        {
            var so = _activeSO;
            var status = _status;
            if (status == null) { End(); return; }

            var input = status.Player != null ? status.InputStatus : null;
            // No input at all (a vessel with no pilot yet) has nothing to grade with; it keeps the
            // multiplier it was armed with until the turn end or a disable ends it.
            if (input == null) return;

            float deviation = StraightLineGesture.DeviationFromFullSpeedStraight(input);
            if (deviation >= so.StraightnessGraceBand) { End(); return; }

            Straightness01 = so.Straightness01(deviation);
            status.BoostMultiplier = so.MultiplierFor(deviation);

            var transformer = status.VesselTransformer;
            if (transformer == null) return;

            // Which way is the speed heading? Ask the transformer for the target it is actually
            // going to use rather than re-deriving throttle x scaler x boost + minimum here —
            // SingleStickVesselTransformer overrides that formula, and a second copy of it would
            // be wrong on whichever vessel adopts this ability next.
            // Against the transformer's OWN tracked speed, never the published status.Speed: that
            // one is after the impact slow, the warp field and flight time scale, so under any of
            // them it disagreed with the number StepTowardTarget actually steps and chose the climb
            // rate for a bleed (or the reverse).
            bool climbing = transformer.CurrentThrottleTarget >= transformer.TrackedSpeed;
            transformer.SetSpeedTrackingRate(climbing ? ClimbRate(so) : so.BleedPerSecond);
        }

        /// <summary>
        /// The wind-up rate, with TIME read LIVE (Time → acceleration, ×1 at rest). It used to be
        /// snapshotted when the ramp first engaged, and a graded ramp re-arms without re-engaging
        /// (<see cref="Begin"/>'s early return), so it can stay engaged for a whole lap: a Time level
        /// earned mid-ramp changed nothing until the pilot let the ramp fall off completely.
        /// </summary>
        float ClimbRate(RampBoostActionSO so) => so.AccelerationPerSecond * so.TimeAccelerationMultiplier(_status);
    }
}
