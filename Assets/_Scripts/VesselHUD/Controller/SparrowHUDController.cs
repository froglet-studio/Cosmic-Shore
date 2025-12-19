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

        Coroutine _heatFillLoop;
        Coroutine _initialAmmoRoutine;
        IVesselStatus _vesselStatus;

        public override void Initialize(IVesselStatus vesselStatus, VesselHUDView baseView)
        {
            base.Initialize(vesselStatus, baseView);
            _vesselStatus = vesselStatus;
            if (!view)
                view = baseView as SparrowHUDView;

            if (!view) return;

            if (!view.isActiveAndEnabled)
                view.gameObject.SetActive(true);

            view.Initialize();
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

                // Initial paint of boost bar to current state
                view.SetBoostState(overheatingExecutor.Heat01, overheatingExecutor.IsOverheating);
            }

            // --- Ammo / missiles ---

            if (fireGunExecutor == null) return;
            fireGunExecutor.OnAmmoChanged += HandleAmmoChanged;
            _initialAmmoRoutine = StartCoroutine(InitialAmmoPaintRoutine());
        }

        void OnDestroy()
        {
            if (_vesselStatus.IsInitializedAsAI || !_vesselStatus.IsLocalUser) return;
            
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

            StopHeatFillLoop();
        }

        #region Initial ammo paint

        private IEnumerator InitialAmmoPaintRoutine()
        {
            yield return null;

            if (view)
                view.InitializeMissileIcon();

            _initialAmmoRoutine = null;
        }

        #endregion

        #region Input block HUD

        private void HandleInputEventBlocked(InputEventBlockPayload payload)
        {
            if (!view) return;
            view.HandleInputEventBlocked(payload);
        }

        #endregion

        #region Weapon mode HUD

        private void HandleStationaryModeChanged(bool isStationary)
        {
            if (!view) return;
            view.SetWeaponMode(isStationary);
        }

        #endregion

        #region Heat / boost HUD

        void OnHeatBuildStarted() => StartHeatFillLoop();
        void OnOverheated()       => StartHeatFillLoop();
        void OnHeatDecayStarted() => StartHeatFillLoop();

        void OnHeatDecayCompleted()
        {
            StopHeatFillLoop();
            if (overheatingExecutor && view)
                view.SetBoostState(overheatingExecutor.Heat01, false);
        }

        void StartHeatFillLoop()
        {
            StopHeatFillLoop();
            _heatFillLoop = StartCoroutine(HeatFillRoutine());
        }

        void StopHeatFillLoop()
        {
            if (_heatFillLoop != null)
            {
                StopCoroutine(_heatFillLoop);
                _heatFillLoop = null;
            }
        }

        private IEnumerator HeatFillRoutine()
        {
            if (!view || !overheatingExecutor) yield break;

            while (true)
            {
                float heat = Mathf.Clamp01(overheatingExecutor.Heat01);
                bool  hot  = overheatingExecutor.IsOverheating;

                view.SetBoostState(heat, hot);

                yield return new WaitForSeconds(0.05f);
            }
        }

        #endregion

        #region Ammo HUD

        private void HandleAmmoChanged(float ammo01)
        {
            if (!view) return;
            view.SetMissilesFromAmmo01(ammo01);
        }

        #endregion
    }
}
