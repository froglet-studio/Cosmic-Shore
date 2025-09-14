using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class SparrowHUDController : ShipHUDController
    {
        [Header("View binding")]
        [SerializeField] private SparrowHUDView view;

        [Header("Executors")]
        [SerializeField] private FireGunActionExecutor fireGunExecutor;
        [SerializeField] private OverheatingActionExecutor overheatingExecutor;

        [Header("Colors")]
        [SerializeField] private Color boostNormalColor;
        [SerializeField] private Color boostFullColor;
        [SerializeField] private Color overheatingColor;

        [Header("Drain animation")]
        [SerializeField] private float drainSpeed = 0.04f;

        Coroutine _heatFillLoop;
        Coroutine _drainLoop;

        public override void Initialize(IVesselStatus vesselStatus, ShipHUDView baseView)
        {
            base.Initialize(vesselStatus, baseView);
            view = view != null ? view : baseView as SparrowHUDView;

            if (view != null && !view.isActiveAndEnabled) 
                view.gameObject.SetActive(true);

            if (overheatingExecutor != null)
            {
                overheatingExecutor.OnHeatBuildStarted   += OnHeatBuildStarted;
                overheatingExecutor.OnOverheated         += OnOverheated;
                overheatingExecutor.OnHeatDecayStarted   += OnHeatDecayStarted;
                overheatingExecutor.OnHeatDecayCompleted += OnHeatDecayCompleted;

                ApplyBoostVisual(overheatingExecutor.Heat01, overheatingExecutor.IsOverheating);
            }
        }

        private void OnDestroy()
        {
            if (overheatingExecutor != null)
            {
                overheatingExecutor.OnHeatBuildStarted   -= OnHeatBuildStarted;
                overheatingExecutor.OnOverheated         -= OnOverheated;
                overheatingExecutor.OnHeatDecayStarted   -= OnHeatDecayStarted;
                overheatingExecutor.OnHeatDecayCompleted -= OnHeatDecayCompleted;
            }
            StopHeatFillLoop();
            StopDrainLoop();
        }

        void OnHeatBuildStarted()
        {
            StopDrainLoop();
            StartHeatFillLoop();
        }

        void OnOverheated()
        {
            StopHeatFillLoop();
            StopDrainLoop();
            ApplyBoostVisual(1f, overheated: true);
        }

        void OnHeatDecayStarted()
        {
            StopHeatFillLoop();
            StartDrainLoop();
        }

        void OnHeatDecayCompleted()
        {
            StopHeatFillLoop();
            StopDrainLoop();
            ApplyBoostVisual(0f, overheated: false);
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

        IEnumerator HeatFillRoutine()
        {
            var img = view?.boostFill;
            if (img == null || overheatingExecutor == null) yield break;

            while (true)
            {
                float heat = Mathf.Clamp01(overheatingExecutor.Heat01);
                bool  hot  = overheatingExecutor.IsOverheating;

                ApplyBoostVisual(heat, hot);
                yield return new WaitForSeconds(0.05f);
            }
        }

        void StartDrainLoop()
        {
            StopDrainLoop();
            _drainLoop = StartCoroutine(DrainToZeroRoutine());
        }

        void StopDrainLoop()
        {
            if (_drainLoop != null)
            {
                StopCoroutine(_drainLoop);
                _drainLoop = null;
            }
        }

        IEnumerator DrainToZeroRoutine()
        {
            var img = view?.boostFill;
            if (img == null) yield break;

            while (img.fillAmount > 0f)
            {
                float next = Mathf.MoveTowards(img.fillAmount, 0f, drainSpeed * Time.deltaTime);
                ApplyBoostVisual(next, overheated: false);
                yield return null;
            }
            ApplyBoostVisual(0f, overheated: false);
        }

        void ApplyBoostVisual(float shown01, bool overheated)
        {
            if (!view?.boostFill) return;

            view.boostFill.fillAmount = Mathf.Clamp01(shown01);
            view.boostFill.color = overheated
                ? overheatingColor
                : Mathf.Approximately(shown01, 1f) ? boostFullColor : boostNormalColor;
        }

        private void Update()
        {
            if (!view) return;
            if (fireGunExecutor && fireGunExecutor.IsInitialized)
                PaintMissilesFromAmmo01(fireGunExecutor.Ammo01);
        }

        private void PaintMissilesFromAmmo01(float ammo01)
        {
            if (!view.missileIcon || view.missileIcons == null || view.missileIcons.Length == 0) return;
            int maxState = view.missileIcons.Length - 1;
            int state = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(ammo01) * maxState), 0, maxState);
            var sprite = view.missileIcons[state];
            if (sprite) 
                view.missileIcon.sprite = sprite;
            view.missileIcon.enabled = true;
        }
    }
}
