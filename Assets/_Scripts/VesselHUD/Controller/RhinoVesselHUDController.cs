using System;
using System.Collections.Generic;
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
        
        readonly HashSet<IVesselStatus> _uniqueSlowedThisExplosion = new();

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
            if (_vesselStatus == null) return;
            VesselChangeSpeedByExplosionEffectSO.OnVesselSlowedByExplosion += HandleVesselSlowedByExplosion;
            SparrowDebuffByRhinoDangerPrismEffectSO.OnVesselSlowedByRhinoDangerPrism += HandleVesselSlowedByExplosion;
            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser) return;

            if (growSkimmerExecutor != null)
                growSkimmerExecutor.OnScaleChanged += HandleSkimmerScaleChanged;

            VesselExplosionByCrystalEffectSO.OnCrystalExplosionTriggered += HandleCrystalExplosion;
            VesselDamageBySkimmerEffectSO.OnSkimmerDebuffApplied += HandleSkimmerDebuffApplied;
        }

        void Unsubscribe()
        {
            if (_vesselStatus == null) return;
            VesselChangeSpeedByExplosionEffectSO.OnVesselSlowedByExplosion -= HandleVesselSlowedByExplosion;
            SparrowDebuffByRhinoDangerPrismEffectSO.OnVesselSlowedByRhinoDangerPrism += HandleVesselSlowedByExplosion;
            if (_vesselStatus.IsInitializedAsAI|| !_vesselStatus.IsLocalUser) return;

            if (growSkimmerExecutor != null)
                growSkimmerExecutor.OnScaleChanged -= HandleSkimmerScaleChanged;

            VesselExplosionByCrystalEffectSO.OnCrystalExplosionTriggered -= HandleCrystalExplosion;
            VesselDamageBySkimmerEffectSO.OnSkimmerDebuffApplied -= HandleSkimmerDebuffApplied;
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
            _uniqueSlowedThisExplosion.Clear();

            view.SetSlowedCount(_slowedCount);
            view.FlashCrystalActivated(2f);
            view.ResetLineIcon();
        }
        
        void HandleVesselSlowedByExplosion(VesselImpactor impactor)
        {
            if (!view) return;

            var victimStatus = impactor?.Vessel?.VesselStatus;
            if (victimStatus != null && _uniqueSlowedThisExplosion.Add(victimStatus))
            {
                _slowedCount++;
                view.SetSlowedCount(_slowedCount);
            }
            
            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser) return;
            view.FlashLineIconActive(2f);
        }
        
        void HandleSkimmerDebuffApplied(IVesselStatus attacker, IVesselStatus victim, float duration)
        {
            if (!view) return;
            if (attacker != _vesselStatus) return;

            view.ShowDebuffTimer(duration);
        }

        #endregion
    }
}
