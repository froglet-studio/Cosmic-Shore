using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    public sealed class RhinoVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private RhinoVesselHUDView view;

        [Header("Rhino – Scene Refs")]
        [SerializeField] private ShieldSkimmerScaleDriver growSkimmerExecutor;

        [Header("Events (SOAP)")]
        [SerializeField] private ScriptableEventVesselImpactor rhinoCrystalExplosionEvent;
        [SerializeField] private ScriptableEventVesselImpactor vesselSlowedByExplosionEvent;
        [SerializeField] private ScriptableEventVesselImpactor vesselSlowedByRhinoDangerPrismEvent;
        [SerializeField] private ScriptableEventSkimmerDebuffApplied skimmerDebuffAppliedEvent;

        private int _slowedCount;
        private IVesselStatus _vesselStatus;

        readonly HashSet<IVesselStatus> _uniqueSlowedThisExplosion = new();

        private bool IsHudAllowed =>
            _vesselStatus is { IsInitializedAsAI: false, IsLocalUser: true };

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _vesselStatus = vesselStatus;

            if (!view)
                view = View as RhinoVesselHUDView;

            view?.Initialize();

            Subscribe();
        }

        void OnDisable()
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

            if (!IsHudAllowed) return;

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

            if (!IsHudAllowed) return;

            if (growSkimmerExecutor != null)
                growSkimmerExecutor.OnScaleChanged -= HandleSkimmerScaleChanged;

            if (skimmerDebuffAppliedEvent != null)
                skimmerDebuffAppliedEvent.OnRaised -= HandleSkimmerDebuffApplied;
        }

        #region Handlers

        void HandleSkimmerScaleChanged(float current, float min, float max)
        {
            if (!IsHudAllowed || !view) return;

            float t = 0f;
            if (max > min)
                t = Mathf.Clamp01((current - min) / (max - min));

            view.SetSkimmerIconScale01(t);
        }

        void HandleCrystalExplosion(VesselImpactor vesselImpactor)
        {
            if (!IsHudAllowed || !view) return;

            _slowedCount = 0;
            _uniqueSlowedThisExplosion.Clear();

            view.SetSlowedCount(_slowedCount);
            view.FlashCrystalActivated(2f);
            view.ResetLineIcon();
        }

        void HandleVesselSlowedByExplosion(VesselImpactor impactor)
        {
            if (!IsHudAllowed || !view) return;

            var victimStatus = impactor?.Vessel?.VesselStatus;
            if (victimStatus != null && _uniqueSlowedThisExplosion.Add(victimStatus))
            {
                _slowedCount++;
                view.SetSlowedCount(_slowedCount);
            }

            view.FlashLineIconActive(2f);
        }

        void HandleSkimmerDebuffApplied(SkimmerDebuffPayload payload)
        {
            if (!IsHudAllowed || !view) return;

            if (payload.Attacker != _vesselStatus) return;

            view.ShowDebuffTimer(payload.Duration);
        }

        #endregion
    }
}
