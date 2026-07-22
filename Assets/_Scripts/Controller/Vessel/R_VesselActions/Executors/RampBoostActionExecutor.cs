using CosmicShore.Core;
using CosmicShore.Data;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Runs a <see cref="RampBoostActionSO"/> while the triggering input is held: sets the
    /// boosted throttle target and puts the transformer into constant-rate speed tracking,
    /// so the vessel accelerates linearly toward top speed instead of the default
    /// exponential lerp. On release the target drops back to the input-driven throttle
    /// speed and tracking switches to the (much faster) return rate; the transformer
    /// auto-reverts to normal smoothing once the speed arrives.
    /// </summary>
    public sealed class RampBoostActionExecutor : ShipActionExecutorBase
    {
        [Inject] AudioSystem _audioSystem;

        [SerializeField] public Obvious.Soap.ScriptableEventNoParam OnMiniGameTurnEnd;

        IVesselStatus _status;
        RampBoostActionSO _activeSO;
        float _restingMultiplier = 1f;

        public bool IsEngaged => _activeSO != null;

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

            End();

            _status = status;
            _activeSO = so;
            _restingMultiplier = status.BoostMultiplier;
            status.IsBoosting = true;
            status.IsStationary = false;
            status.BoostMultiplier = so.MaxBoostMultiplier;

            // Element → parameter (Time → acceleration). Anchored at 1x at resting level;
            // a high Time element winds the Rhino up to top speed faster.
            float timeMultiplier = _status.ElementalAbilityHandler != null
                ? _status.ElementalAbilityHandler.Multiplier(Element.Time)
                : 1f;
            var transformer = status.VesselTransformer;
            if (transformer != null)
                transformer.SetSpeedTrackingRate(so.AccelerationPerSecond * timeMultiplier);

            _audioSystem.PlayGameplaySFX(so.EngageSFX);
        }

        public void End()
        {
            if (_activeSO == null) return;

            var so = _activeSO;
            _activeSO = null;

            if (_status == null) return;

            _status.BoostMultiplier = _restingMultiplier;
            _status.IsBoosting = false;

            // Fast constant-rate return to the input-driven throttle speed; the transformer
            // reverts to its normal smoothing once the speed lands on the target.
            var transformer = _status.VesselTransformer;
            if (transformer != null)
                transformer.SetSpeedTrackingRate(so.ReturnPerSecond);
        }
    }
}
