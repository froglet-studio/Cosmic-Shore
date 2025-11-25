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

        public override void Initialize(IVesselStatus vesselStatus, VesselHUDView baseView)
        {
            base.Initialize(vesselStatus, baseView);

            if (!view)
                view = baseView as RhinoVesselHUDView;

            if (!growSkimmerExecutor && vesselStatus != null)
            {
                growSkimmerExecutor = vesselStatus.ShipTransform
                    ? vesselStatus.ShipTransform.GetComponentInChildren<GrowSkimmerActionExecutor>(true)
                    : null;
            }

            Subscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        void Subscribe()
        {
            if (growSkimmerExecutor != null)
                growSkimmerExecutor.OnScaleChanged += HandleSkimmerScaleChanged;

            VesselExplosionByCrystalEffectSO.OnCrystalExplosionTriggered += HandleCrystalExplosion;
            VesselChangeSpeedByExplosionEffectSO.OnVesselSlowedByExplosion += HandleVesselSlowedByExplosion;
        }

        void Unsubscribe()
        {
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

        // Crystal collision → explosion spawn
        void HandleCrystalExplosion(VesselImpactor vesselImpactor)
        {
            if (!view) return;

            // Optional: filter to this vessel only, if you have _vesselStatus in base
            // if (vesselImpactor.Vessel.VesselStatus != _vesselStatus) return;

            _slowedCount = 0;
            view.SetSlowedCount(_slowedCount);

            // Turn crystal green, show text, auto-reset & hide text in 2 seconds
            view.FlashCrystalActivated(2f);

            // Line stays neutral until we actually slow someone
            view.ResetLineIcon();
        }

        // Explosion slows ships
        void HandleVesselSlowedByExplosion(VesselImpactor impactor, ExplosionImpactor impactee)
        {
            if (!view) return;

            // Optional: filter to this vessel only
            // if (impactor.Vessel.VesselStatus != _vesselStatus) return;

            _slowedCount++;
            view.SetSlowedCount(_slowedCount);

            // Line icon red → back to normal in 2 seconds
            view.FlashLineIconActive(2f);
        }

        #endregion
    }
}
