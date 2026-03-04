using System.Collections;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SparrowHUDController : VesselHUDController
    {
        [Header("View binding")]
        [SerializeField] private SparrowHUDView view;

        [Header("Executors")]
        [SerializeField] private FireGunActionExecutor fireGunExecutor;
        [SerializeField] private OverheatingActionExecutor overheatingExecutor;

        [Header("Events")]
        [SerializeField] private ScriptableEventBool stationaryModeChanged;
        [SerializeField] private ScriptableEventInputEventBlock onInputEventBlocked;

        Coroutine _initialAmmoRoutine;
        IVesselStatus _vesselStatus;
        bool _heatActive;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _vesselStatus = vesselStatus;

            if (!view)
                view = View as SparrowHUDView;

            if (!view) return;

            Subscribe();
        }

        void Subscribe()
        {
            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser) return;

            if (stationaryModeChanged)
            {
                stationaryModeChanged.OnRaised += HandleStationaryModeChanged;
                HandleStationaryModeChanged(_vesselStatus.IsTranslationRestricted);
            }

            if (onInputEventBlocked)
                onInputEventBlocked.OnRaised += HandleInputEventBlocked;

            if (overheatingExecutor)
            {
                overheatingExecutor.OnHeatBuildStarted   += OnHeatBuildStarted;
                overheatingExecutor.OnOverheated         += OnOverheated;
                overheatingExecutor.OnHeatDecayStarted   += OnHeatDecayStarted;
                overheatingExecutor.OnHeatDecayCompleted += OnHeatDecayCompleted;

                view.SetBoostState(overheatingExecutor.Heat01, overheatingExecutor.IsOverheating);
            }

            if (fireGunExecutor == null) return;
            fireGunExecutor.OnAmmoChanged += HandleAmmoChanged;
            _initialAmmoRoutine = StartCoroutine(InitialAmmoPaintRoutine());
        }

        void OnDisable()
        {
            if (_vesselStatus != null && (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser))
                return;

            if (overheatingExecutor)
            {
                overheatingExecutor.OnHeatBuildStarted   -= OnHeatBuildStarted;
                overheatingExecutor.OnOverheated         -= OnOverheated;
                overheatingExecutor.OnHeatDecayStarted   -= OnHeatDecayStarted;
                overheatingExecutor.OnHeatDecayCompleted -= OnHeatDecayCompleted;
            }

            if (stationaryModeChanged)
                stationaryModeChanged.OnRaised -= HandleStationaryModeChanged;

            if (onInputEventBlocked)
                onInputEventBlocked.OnRaised -= HandleInputEventBlocked;

            if (fireGunExecutor != null)
                fireGunExecutor.OnAmmoChanged -= HandleAmmoChanged;

            if (_initialAmmoRoutine != null)
                StopCoroutine(_initialAmmoRoutine);

            _heatActive = false;
        }

        void Update()
        {
            if (!_heatActive || !view || !overheatingExecutor) return;

            view.SetBoostState(
                Mathf.Clamp01(overheatingExecutor.Heat01),
                overheatingExecutor.IsOverheating);
        }

        private IEnumerator InitialAmmoPaintRoutine()
        {
            yield return null;
            view?.InitializeMissileIcon();
            _initialAmmoRoutine = null;
        }

        private void HandleInputEventBlocked(InputEventBlockPayload payload)
        {
            if (!view) return;
            view.HandleInputEventBlocked(payload);
        }

        private void HandleStationaryModeChanged(bool isStationary)
        {
            if (!view) return;
            view.SetWeaponMode(isStationary);
        }

        void OnHeatBuildStarted() => _heatActive = true;
        void OnOverheated()       => _heatActive = true;
        void OnHeatDecayStarted() => _heatActive = true;

        void OnHeatDecayCompleted()
        {
            _heatActive = false;
            if (overheatingExecutor && view)
                view.SetBoostState(overheatingExecutor.Heat01, false);
        }

        private void HandleAmmoChanged(float ammo01)
        {
            if (!view) return;
            view.SetMissilesFromAmmo01(ammo01);
        }
    }
}
