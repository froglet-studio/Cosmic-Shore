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
    /// <para><b>Only the machine that drives the input grades.</b> Press and release round-trip
    /// through <c>R_VesselActionHandler</c>, so this executor is live on every peer, but a remote
    /// replica's <c>InputStatus</c> is never written and would read as "hard over" forever. The
    /// gate is <see cref="IPlayer.IsNetworkOwner"/> rather than <c>IsLocalUser</c>, because an AI
    /// Rhino's input IS driven — on the server (the same distinction
    /// <c>Player.ReportCombatHit_ServerRpc</c> records). A non-owning peer keeps the pre-grading
    /// behaviour: full multiplier while armed, which is what its replicated transform already
    /// implies.</para>
    /// </summary>
    public sealed class RampBoostActionExecutor : ShipActionExecutorBase
    {
        [Inject] AudioSystem _audioSystem;

        [SerializeField] public Obvious.Soap.ScriptableEventNoParam OnMiniGameTurnEnd;

        IVesselStatus _status;
        RampBoostActionSO _activeSO;
        float _restingMultiplier = 1f;
        float _timeMultiplier = 1f;

        public bool IsEngaged => _activeSO != null;

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
            _restingMultiplier = status.BoostMultiplier;
            status.IsBoosting = true;
            status.IsStationary = false;

            // Element → parameter (Time → acceleration). Anchored at 1x at resting level;
            // a high Time element winds the Rhino up to top speed faster. Snapshotted at engage
            // because it is read every frame below and EvaluateLive is not free.
            _timeMultiplier = _status.ElementalAbilityHandler != null
                ? _status.ElementalAbilityHandler.Multiplier(Element.Time)
                : 1f;

            Straightness01 = 1f;
            status.BoostMultiplier = so.MaxBoostMultiplier;
            var transformer = status.VesselTransformer;
            if (transformer != null)
                transformer.SetSpeedTrackingRate(so.AccelerationPerSecond * _timeMultiplier);

            _audioSystem.PlayGameplaySFX(so.EngageSFX);
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
            // A peer that does not drive this vessel's input has nothing to grade with; leave it
            // on the full multiplier it was armed with rather than reading an empty InputStatus
            // as "hard over".
            if (input == null || !status.Player.IsNetworkOwner) return;

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
            bool climbing = transformer.CurrentThrottleTarget >= status.Speed;
            transformer.SetSpeedTrackingRate(climbing
                ? so.AccelerationPerSecond * _timeMultiplier
                : so.BleedPerSecond);
        }
    }
}
