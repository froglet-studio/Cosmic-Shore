using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class RhinoVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private RhinoVesselHUDView view;

        [Header("Rhino – Scene Refs")]
        [SerializeField] private GrowSkimmerActionExecutor growSkimmerExecutor;

        int _slowedCount;
        private IVesselStatus _vesselStatus;

        public override void Initialize(IVesselStatus vesselStatus, VesselHUDView baseView)
        {
            base.Initialize(vesselStatus, baseView);
            _vesselStatus = vesselStatus;
            if (!view)
                view = baseView as RhinoVesselHUDView;

            Subscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        void Subscribe()
        {
            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsNetworkOwner) return;
            if (growSkimmerExecutor != null)
                growSkimmerExecutor.OnScaleChanged += HandleSkimmerScaleChanged;

            VesselExplosionByCrystalEffectSO.OnCrystalExplosionTriggered += HandleCrystalExplosion;
            VesselChangeSpeedByExplosionEffectSO.OnVesselSlowedByExplosion += HandleVesselSlowedByExplosion;
        }

        void Unsubscribe()
        {
            if (_vesselStatus.IsInitializedAsAI) return;
            if (growSkimmerExecutor != null)
                growSkimmerExecutor.OnScaleChanged -= HandleSkimmerScaleChanged;

            VesselExplosionByCrystalEffectSO.OnCrystalExplosionTriggered -= HandleCrystalExplosion;
            VesselChangeSpeedByExplosionEffectSO.OnVesselSlowedByExplosion -= HandleVesselSlowedByExplosion;
        }

        #region Handlers

        void HandleSkimmerScaleChanged(float current, float min, float max)
        {
            if (!view) return;

            if (Mathf.Approximately(max, min))
            {
                view.SetSkimmerIconScale01(0f);
                return;
            }

            var t = Mathf.InverseLerp(min, max, current);
            view.SetSkimmerIconScale01(t);
        }

        void HandleCrystalExplosion(VesselImpactor vesselImpactor)
        {
            if (!view) return;
            
            _slowedCount = 0;
            
            view.SetSlowedCount(_slowedCount);
            view.FlashCrystalActivated(2f);
            view.ResetLineIcon();
        }

        void HandleVesselSlowedByExplosion(VesselImpactor impactor, ExplosionImpactor impactee)
        {
            if (!view) return;

            _slowedCount++;
            
            view.SetSlowedCount(_slowedCount);
            view.FlashLineIconActive(2f);
        }

        #endregion
    }
}
