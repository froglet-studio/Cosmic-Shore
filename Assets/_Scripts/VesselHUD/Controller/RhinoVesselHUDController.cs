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

        [Header("Events (SOAP)")]
        [SerializeField] private ScriptableEventVesselImpactor rhinoCrystalExplosionEvent;
        [SerializeField] private ScriptableEventVesselImpactor vesselSlowedByExplosionEvent;
        [SerializeField] private ScriptableEventVesselImpactor vesselSlowedByRhinoDangerPrismEvent;
        [SerializeField] private ScriptableEventSkimmerDebuffApplied skimmerDebuffAppliedEvent;

        int _slowedCount;
        IVesselStatus _vesselStatus;

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
            
            if (vesselSlowedByExplosionEvent != null)
                vesselSlowedByExplosionEvent.OnRaised += HandleVesselSlowedByExplosion;

            if (vesselSlowedByRhinoDangerPrismEvent != null)
                vesselSlowedByRhinoDangerPrismEvent.OnRaised += HandleVesselSlowedByExplosion;

            if (rhinoCrystalExplosionEvent != null)
                rhinoCrystalExplosionEvent.OnRaised += HandleCrystalExplosion;

            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser) return;

            if (growSkimmerExecutor != null)
                growSkimmerExecutor.OnScaleChanged += HandleSkimmerScaleChanged;

            if (skimmerDebuffAppliedEvent != null)
                skimmerDebuffAppliedEvent.OnRaised += HandleSkimmerDebuffApplied;
        }

        void Unsubscribe()
        {
            if (_vesselStatus == null) return;

            if (vesselSlowedByExplosionEvent != null)
                vesselSlowedByExplosionEvent.OnRaised -= HandleVesselSlowedByExplosion;

            if (vesselSlowedByRhinoDangerPrismEvent != null)
                vesselSlowedByRhinoDangerPrismEvent.OnRaised -= HandleVesselSlowedByExplosion;

            if (rhinoCrystalExplosionEvent != null)
                rhinoCrystalExplosionEvent.OnRaised -= HandleCrystalExplosion;

            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser) return;

            if (growSkimmerExecutor != null)
                growSkimmerExecutor.OnScaleChanged -= HandleSkimmerScaleChanged;

            if (skimmerDebuffAppliedEvent != null)
                skimmerDebuffAppliedEvent.OnRaised -= HandleSkimmerDebuffApplied;
        }

        #region Handlers

        void HandleSkimmerScaleChanged(float current, float min, float max) { }

        void HandleCrystalExplosion(VesselImpactor vesselImpactor)
        {
            if (!view) return;
            if(_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser) return;

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

        void HandleSkimmerDebuffApplied(SkimmerDebuffPayload payload)
        {
            if (!view) return;
            if (payload.Attacker != _vesselStatus) return;

            view.ShowDebuffTimer(payload.Duration);
        }

        #endregion
    }
}
