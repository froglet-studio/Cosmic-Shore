using System.Collections;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.UI;
namespace CosmicShore.UI
{
    public class SparrowHUDController : VesselHUDController
    {
        [Header("View binding")]
        [SerializeField] private SparrowHUDView view;

        [Header("Executors")]
        [SerializeField] private FireGunActionExecutor fireGunExecutor;

        [Tooltip("Drives the boost ability icon's charge ring: the Sparrow's boost is indefinite, so " +
                 "the icon shows whether the once-per-press strafing roll is still available instead " +
                 "of a heat/boost gauge.")]
        [SerializeField] private BarrelRollController barrelRollController;

        [Header("Events")]
        [SerializeField] private ScriptableEventBool stationaryModeChanged;
        [SerializeField] private ScriptableEventInputEventBlock onInputEventBlocked;

        Coroutine _initialAmmoRoutine;
        IVesselStatus _vesselStatus;

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

            // Detach-first: a vessel swap re-runs Initialize on live components, and a second
            // subscribe would double every callback.
            Unsubscribe();

            if (stationaryModeChanged)
            {
                stationaryModeChanged.OnRaised += HandleStationaryModeChanged;
                HandleStationaryModeChanged(_vesselStatus.IsTranslationRestricted);
            }

            if (onInputEventBlocked)
                onInputEventBlocked.OnRaised += HandleInputEventBlocked;

            if (barrelRollController)
            {
                barrelRollController.OnRollChargeChanged += HandleRollChargeChanged;
                view.SetRollCharge(barrelRollController.IsRollArmed);
            }

            if (fireGunExecutor == null) return;
            fireGunExecutor.OnAmmoChanged += HandleAmmoChanged;
            _initialAmmoRoutine = StartCoroutine(InitialAmmoPaintRoutine());
        }

        void OnDisable()
        {
            if (_vesselStatus != null && (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser))
                return;

            Unsubscribe();

            if (_initialAmmoRoutine != null)
            {
                StopCoroutine(_initialAmmoRoutine);
                _initialAmmoRoutine = null;
            }
        }

        void Unsubscribe()
        {
            if (barrelRollController)
                barrelRollController.OnRollChargeChanged -= HandleRollChargeChanged;

            if (stationaryModeChanged)
                stationaryModeChanged.OnRaised -= HandleStationaryModeChanged;

            if (onInputEventBlocked)
                onInputEventBlocked.OnRaised -= HandleInputEventBlocked;

            if (fireGunExecutor != null)
                fireGunExecutor.OnAmmoChanged -= HandleAmmoChanged;
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

        private void HandleRollChargeChanged(bool armed)
        {
            if (!view) return;
            view.SetRollCharge(armed);
        }

        private void HandleAmmoChanged(float ammo01)
        {
            if (!view) return;
            view.SetMissilesFromAmmo01(ammo01);
        }
    }
}
