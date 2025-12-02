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

        // Track unique victims per explosion
        readonly HashSet<IVesselStatus> _uniqueSlowedThisExplosion = new HashSet<IVesselStatus>();

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
            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser) return;

            if (growSkimmerExecutor != null)
                growSkimmerExecutor.OnScaleChanged += HandleSkimmerScaleChanged;

            VesselExplosionByCrystalEffectSO.OnCrystalExplosionTriggered += HandleCrystalExplosion;
            VesselChangeSpeedByExplosionEffectSO.OnVesselSlowedByExplosion += HandleVesselSlowedByExplosion;
            VesselDamageBySkimmerEffectSO.OnSkimmerDebuffApplied += HandleSkimmerDebuffApplied;
        }

        void Unsubscribe()
        {
            if (_vesselStatus == null) return;
            if (_vesselStatus.IsInitializedAsAI) return;

            if (growSkimmerExecutor != null)
                growSkimmerExecutor.OnScaleChanged -= HandleSkimmerScaleChanged;

            VesselExplosionByCrystalEffectSO.OnCrystalExplosionTriggered -= HandleCrystalExplosion;
            VesselChangeSpeedByExplosionEffectSO.OnVesselSlowedByExplosion -= HandleVesselSlowedByExplosion;
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

        // When crystal explosion spawns
        void HandleCrystalExplosion(VesselImpactor vesselImpactor)
        {
            if (!view) return;

            // Optional: only if this explosion belongs to our Rhino
            // if (vesselImpactor.Vessel.VesselStatus != _vesselStatus) return;

            _slowedCount = 0;
            _uniqueSlowedThisExplosion.Clear();

            view.SetSlowedCount(_slowedCount);
            view.FlashCrystalActivated(2f);
            view.ResetLineIcon();
        }

        // When explosion slows ships
        void HandleVesselSlowedByExplosion(VesselImpactor impactor, ExplosionImpactor impactee)
        {
            if (!view) return;

            // Optional: ensure explosion came from our Rhino by checking its source vessel if available
            // if (impactee.SourceVesselStatus != _vesselStatus) return;

            var victimStatus = impactor?.Vessel?.VesselStatus;
            if (victimStatus != null && !_uniqueSlowedThisExplosion.Contains(victimStatus))
            {
                _uniqueSlowedThisExplosion.Add(victimStatus);
                _slowedCount++;
                view.SetSlowedCount(_slowedCount);
            }

            // Even if already slowed, we can still re-flash the line icon
            view.FlashLineIconActive(2f);
        }

        // When our Rhino’s skimmer applies input debuff
        void HandleSkimmerDebuffApplied(IVesselStatus attacker, IVesselStatus victim, float duration)
        {
            if (!view) return;
            if (attacker != _vesselStatus) return; // only show for this Rhino

            view.ShowDebuffTimer(duration);
        }

        #endregion
    }
}
