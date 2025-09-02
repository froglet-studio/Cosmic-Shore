using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class SparrowHUDController : R_ShipHUDController
    {
        [Header("View binding")]
        [SerializeField] private SparrowHUDView view;

        [Header("Actions")]
        [SerializeField] private FireGunAction fireGunAction;
        [SerializeField] private OverheatingAction overheatingAction;

        [Header("Colors")]
        [SerializeField] private Color boostNormalColor;
        [SerializeField] private Color boostFullColor;
        [SerializeField] private Color overheatingColor;

        [Header("Drain animation")]
        [SerializeField] private float drainSpeed = 0.04f;

        Coroutine _heatFillLoop;
        Coroutine _drainLoop;

        public override void Initialize(IShipStatus shipStatus, R_ShipHUDView baseView)
        {
            base.Initialize(shipStatus, baseView);
            view = view != null ? view : baseView as SparrowHUDView;
            
            if(view != null && !view.isActiveAndEnabled) view.gameObject.SetActive(true);

            if (overheatingAction != null)
            {
                overheatingAction.OnHeatBuildStarted += OnHeatBuildStarted;
                overheatingAction.OnOverheated += OnOverheated;
                overheatingAction.OnHeatDecayStarted += OnHeatDecayStarted;
                overheatingAction.OnHeatDecayCompleted += OnHeatDecayCompleted;
            }
            ApplyBoostVisual(overheatingAction ? overheatingAction.Heat01 : 0f, overheated: overheatingAction && overheatingAction.IsOverheating);
        }

        private void OnDestroy()
        {
            if (overheatingAction != null)
            {
                overheatingAction.OnHeatBuildStarted   -= OnHeatBuildStarted;
                overheatingAction.OnOverheated         -= OnOverheated;
                overheatingAction.OnHeatDecayStarted   -= OnHeatDecayStarted;
                overheatingAction.OnHeatDecayCompleted -= OnHeatDecayCompleted;
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
            if (img == null || overheatingAction == null) yield break;

            while (true)
            {
                float heat = Mathf.Clamp01(overheatingAction.Heat01);
                bool  hot  = overheatingAction.IsOverheating;

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
            if (view?.boostFill == null) return;

            view.boostFill.fillAmount = Mathf.Clamp01(shown01);
            view.boostFill.color = overheated
                ? overheatingColor
                : Mathf.Approximately(shown01, 1f) ? boostFullColor : boostNormalColor;
        }
        
        private void Update()
        {
            if (view == null) return;
            if (fireGunAction != null)
                PaintMissilesFromAmmo01(fireGunAction.Ammo01);
        }

        private void PaintMissilesFromAmmo01(float ammo01)
        {
            if (view.missileIcon == null || view.missileIcons == null || view.missileIcons.Length == 0) return;
            int maxState = view.missileIcons.Length - 1;
            int state = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(ammo01) * maxState), 0, maxState);
            var sprite = view.missileIcons[state];
            if (sprite != null) view.missileIcon.sprite = sprite;
            view.missileIcon.enabled = true;
        }
    }
}
