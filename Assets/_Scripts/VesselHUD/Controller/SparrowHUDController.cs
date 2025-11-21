using System.Collections;
using System.Collections.Generic;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class SparrowHUDController : VesselHUDController
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
        [SerializeField] private Color blockedInputColor = Color.red;   // NEW

        [Header("Drain animation")]
        [SerializeField] private float drainSpeed = 0.04f;

        [Header("Events")]
        [SerializeField] private ScriptableEventBool stationaryModeChanged; 
        [SerializeField] private ScriptableEventInputEventBlock onInputEventBlocked; // NEW

        Coroutine _heatFillLoop;
        Coroutine _drainLoop;

        // NEW: track block + pressed states, and defaults to restore
        private readonly HashSet<InputEvents> _blocked = new();
        private readonly HashSet<InputEvents> _pressed = new();
        private readonly Dictionary<InputEvents, Color> _defaultHighlightColors = new();
        private readonly Dictionary<InputEvents, Coroutine> _blockEnforcers = new();
        // keep a local reference to the handler so we can subscribe in addition to the base
        private R_VesselActionHandler _handler; // NEW

        public override void Initialize(IVesselStatus vesselStatus, VesselHUDView baseView)
        {
            base.Initialize(vesselStatus, baseView);
            view = view ? view : baseView as SparrowHUDView;

            if (view && !view.isActiveAndEnabled) view.gameObject.SetActive(true);

            if (stationaryModeChanged) stationaryModeChanged.OnRaised += HandleStationaryModeChanged;
            HandleStationaryModeChanged(vesselStatus.IsTranslationRestricted);

            if (onInputEventBlocked) onInputEventBlocked.OnRaised += HandleInputEventBlocked;

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
            if (stationaryModeChanged)  stationaryModeChanged.OnRaised  -= HandleStationaryModeChanged;
            if (onInputEventBlocked)    onInputEventBlocked.OnRaised    -= HandleInputEventBlocked;
            
            StopHeatFillLoop();
            StopDrainLoop();
        }

        // ---------- Block logic ----------

        private void HandleInputEventBlocked(InputEventBlockPayload p)
        {
            if (!view || view.highlights == null) return;

            var image = FindHighlightImage(p.Input);
            if (!image) return;

            if (p.Started)
            {
                // (Re)start enforcer for this input
                if (_blockEnforcers.TryGetValue(p.Input, out var running) && running != null)
                    StopCoroutine(running);
                _blockEnforcers[p.Input] = StartCoroutine(EnforceBlockedHighlight(image, p.Input));
            }
            else if (p.Ended)
            {
                // Stop enforcer + hard restore (white & hidden)
                if (_blockEnforcers.TryGetValue(p.Input, out var running) && running != null)
                    StopCoroutine(running);
                _blockEnforcers.Remove(p.Input);

                image.color   = Color.white; // restore to white explicitly
                image.enabled = false;       // hide exactly when mute ends
            }
        }

        private IEnumerator EnforceBlockedHighlight(Image img, InputEvents input)
        {
            // while no "ended" event has come in, keep forcing red+enabled each frame
            // (We don't poll any state; the End event will stop this.)
            while (true)
            {
                if (!img) yield break;
                img.enabled = true;
                img.color   = blockedInputColor;
                yield return null;
            }
        }

        private Image FindHighlightImage(InputEvents ev)
        {
            for (int i = 0; i < view.highlights.Count; i++)
                if (view.highlights[i].input == ev)
                    return view.highlights[i].image;
            return null;
        }

        // ---------- existing HUD behavior below (unchanged) ----------

        private void HandleStationaryModeChanged(bool isStationary)
        {
            if (!view || !view.weaponModeIcon || view.weaponModeIcons == null || view.weaponModeIcons.Length < 2)
                return;

            int idx = isStationary ? 1 : 0;
            var sprite = view.weaponModeIcons[idx];
            if (sprite != null)
            {
                view.weaponModeIcon.sprite = sprite;
                view.weaponModeIcon.enabled = true;
            }
        }

        void OnHeatBuildStarted()      { StopDrainLoop(); StartHeatFillLoop(); }
        void OnOverheated()            { StopHeatFillLoop(); StopDrainLoop(); ApplyBoostVisual(1f, true); }
        void OnHeatDecayStarted()      { StopHeatFillLoop(); StartDrainLoop(); }
        void OnHeatDecayCompleted()    { StopHeatFillLoop(); StopDrainLoop(); ApplyBoostVisual(0f, false); }

        void StartHeatFillLoop() { StopHeatFillLoop(); _heatFillLoop = StartCoroutine(HeatFillRoutine()); }
        void StopHeatFillLoop()  { if (_heatFillLoop != null) { StopCoroutine(_heatFillLoop); _heatFillLoop = null; } }

        IEnumerator HeatFillRoutine()
        {
            var img = view?.boostFill;
            if (!img || !overheatingExecutor) yield break;

            while (true)
            {
                float heat = Mathf.Clamp01(overheatingExecutor.Heat01);
                bool  hot  = overheatingExecutor.IsOverheating;
                ApplyBoostVisual(heat, hot);
                yield return new WaitForSeconds(0.05f);
            }
        }

        void StartDrainLoop() { StopDrainLoop(); _drainLoop = StartCoroutine(DrainToZeroRoutine()); }
        void StopDrainLoop()  { if (_drainLoop != null) { StopCoroutine(_drainLoop); _drainLoop = null; } }

        IEnumerator DrainToZeroRoutine()
        {
            var img = view?.boostFill;
            if (!img) yield break;

            while (img.fillAmount > 0f)
            {
                float next = Mathf.MoveTowards(img.fillAmount, 0f, drainSpeed * Time.deltaTime);
                ApplyBoostVisual(next, false);
                yield return null;
            }
            ApplyBoostVisual(0f, false);
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
            if (fireGunExecutor)
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
