using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SerpentVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private SerpentVesselHUDView view;

        [Header("Boost (charges)")]
        [SerializeField] private ConsumeBoostActionExecutor consumeBoostExecutor;

        [Header("Shields")]
        [SerializeField] private int shieldResourceIndex;

        IVesselStatus  _status;
        ResourceSystem _rs;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _status = vesselStatus;

            if (!view)
                view = View as SerpentVesselHUDView;

            if (!view) return;

            Subscribe();
        }


        void Subscribe()
        {
            if (_status.IsInitializedAsAI || !_status.IsLocalUser) return;
            
            if (consumeBoostExecutor == null)
            {
                var registry = _status?.ShipTransform
                    ? _status.ShipTransform.GetComponentInChildren<ActionExecutorRegistry>(true)
                    : null;

                if (registry != null)
                    consumeBoostExecutor = registry.Get<ConsumeBoostActionExecutor>();
            }

            _rs = _status?.ResourceSystem;
            if (_rs != null)
            {
                _rs.OnResourceChanged += HandleResourceChanged;
                PushInitialShields();
            }

            if (consumeBoostExecutor == null || view == null) return;
            consumeBoostExecutor.OnChargesSnapshot += HandleBoostSnapshot;
            consumeBoostExecutor.OnChargeConsumed  += HandleBoostChargeConsumed;

            HandleBoostSnapshot(
                consumeBoostExecutor.AvailableCharges,
                consumeBoostExecutor.MaxCharges
            );
        }

        void OnDisable()
        {
            if (_rs != null)
                _rs.OnResourceChanged -= HandleResourceChanged;

            if (consumeBoostExecutor != null)
            {
                consumeBoostExecutor.OnChargesSnapshot -= HandleBoostSnapshot;
                consumeBoostExecutor.OnChargeConsumed  -= HandleBoostChargeConsumed;
            }

            if (view != null)
                view.ResetBoostPips();
        }

        // ---------- Shields ----------

        void HandleResourceChanged(int index, float current, float max)
        {
            if (!view) return;
            if (index != shieldResourceIndex || max <= 0f) return;

            var norm   = Mathf.Clamp01(current / max);
            var   shields = Mathf.Clamp(Mathf.FloorToInt(norm * 4f + 0.0001f), 0, 4);
            view.SetShieldCount(shields);
        }

        void PushInitialShields()
        {
            if (_rs == null || view == null) return;
            if ((uint)shieldResourceIndex >= _rs.Resources.Count) return;

            var r    = _rs.Resources[shieldResourceIndex];
            var norm = (r.MaxAmount <= 0f) ? 0f : Mathf.Clamp01(r.CurrentAmount / r.MaxAmount);
            var shields = Mathf.Clamp(Mathf.FloorToInt(norm * 4f), 0, 4);

            view.SetShieldCount(shields);
        }

        // ---------- Boost pips ----------

        void HandleBoostSnapshot(int available, int max)
        {
            if (!view) return;
            view.ApplyBoostSnapshot(available, max);
        }

        void HandleBoostChargeConsumed(int pipIndex, float duration)
        {
            if (!view) return;
            view.AnimateBoostChargeConsumed(pipIndex, duration);
        }
    }
}