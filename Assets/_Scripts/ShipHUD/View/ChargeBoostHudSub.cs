using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ChargeBoostHudSub", menuName = "HUD/Subs/ChargeBoost")]
    public class ChargeBoostHudSub : HudSubscriptionSO
    {
        [SerializeField] private int meterIndex = 0;
        [SerializeField] private float chargeSeconds = 0.6f;     
        [SerializeField] private float dischargeSeconds = 0.6f; 

        private ChargeBoostAction _action;

        private System.Action<float> _onChargeStart;
        private System.Action<float> _onChargeProgress;
        private System.Action        _onChargeEnd;
        private System.Action<float> _onDischargeStart;
        private System.Action<float> _onDischargeProgress;
        private System.Action        _onDischargeEnd;

        protected override void OnEnableSubscriptions()
        {
            _action = Refs ? Refs.chargeBoost : null;
            if (_action == null || Effects == null) return;

            // One smooth animation per phase; ignore per-tick spam so the tween isn’t constantly restarted.
            _onChargeStart       = _ => Effects.AnimateRefill(meterIndex, chargeSeconds, 1f);
            _onChargeProgress    = _ => {  };
            _onChargeEnd         = () => Effects.AnimateRefill(meterIndex, 0.05f, 1f); // snap-finish

            _onDischargeStart    = _ => Effects.AnimateDrain(meterIndex, dischargeSeconds, 0f);
            _onDischargeProgress = _ => {  };
            _onDischargeEnd      = () => Effects.AnimateDrain(meterIndex, 0.05f, 0f);  // snap-finish

            _action.OnChargeStarted     += _onChargeStart;
            _action.OnChargeProgress    += _onChargeProgress;
            _action.OnChargeEnded       += _onChargeEnd;
            _action.OnDischargeStarted  += _onDischargeStart;
            _action.OnDischargeProgress += _onDischargeProgress;
            _action.OnDischargeEnded    += _onDischargeEnd;
        }

        protected override void OnDisableSubscriptions()
        {
            if (_action == null) return;

            if (_onChargeStart       != null) _action.OnChargeStarted     -= _onChargeStart;
            if (_onChargeProgress    != null) _action.OnChargeProgress    -= _onChargeProgress;
            if (_onChargeEnd         != null) _action.OnChargeEnded       -= _onChargeEnd;
            if (_onDischargeStart    != null) _action.OnDischargeStarted  -= _onDischargeStart;
            if (_onDischargeProgress != null) _action.OnDischargeProgress -= _onDischargeProgress;
            if (_onDischargeEnd      != null) _action.OnDischargeEnded    -= _onDischargeEnd;

            _onChargeStart = null; _onChargeProgress = null; _onChargeEnd = null;
            _onDischargeStart = null; _onDischargeProgress = null; _onDischargeEnd = null;
            _action = null;
        }
    }
}
