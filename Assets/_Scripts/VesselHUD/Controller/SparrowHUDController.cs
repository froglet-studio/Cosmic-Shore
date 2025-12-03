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
        [SerializeField] private Color blockedInputColor = Color.red;

        [Header("Events")]
        [SerializeField] private ScriptableEventBool stationaryModeChanged;
        [SerializeField] private ScriptableEventInputEventBlock onInputEventBlocked;

        Coroutine _heatFillLoop;
        Coroutine _initialAmmoRoutine;

        private readonly Dictionary<InputEvents, Coroutine> _blockEnforcers = new();

        public override void Initialize(IVesselStatus vesselStatus, VesselHUDView baseView)
        {
            base.Initialize(vesselStatus, baseView);
            view = view ? view : baseView as SparrowHUDView;

            if (view && !view.isActiveAndEnabled) 
                view.gameObject.SetActive(true);

            if (stationaryModeChanged) 
                stationaryModeChanged.OnRaised += HandleStationaryModeChanged;
            HandleStationaryModeChanged(vesselStatus.IsTranslationRestricted);

            if (onInputEventBlocked) 
                onInputEventBlocked.OnRaised += HandleInputEventBlocked;

            if (overheatingExecutor != null)
            {
                overheatingExecutor.OnHeatBuildStarted   += OnHeatBuildStarted;
                overheatingExecutor.OnOverheated         += OnOverheated;
                overheatingExecutor.OnHeatDecayStarted   += OnHeatDecayStarted;
                overheatingExecutor.OnHeatDecayCompleted += OnHeatDecayCompleted;

                ApplyBoostVisual(overheatingExecutor.Heat01, overheatingExecutor.IsOverheating);
            }

            if (fireGunExecutor == null) return;
            fireGunExecutor.OnAmmoChanged += HandleAmmoChanged;
            _initialAmmoRoutine = StartCoroutine(InitialAmmoPaintRoutine());
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

        // ---------- Initial ammo paint ----------

        private IEnumerator InitialAmmoPaintRoutine()
        {
            yield return null;

            var sprite = view.missileIcons[2];
            if (sprite)
                view.missileIcon.sprite = sprite;

            _initialAmmoRoutine = null;
        }

        // ---------- Block logic (unchanged) ----------

        private void HandleInputEventBlocked(InputEventBlockPayload p)
        {
            if (!view || view.highlights == null) return;

            var image = FindHighlightImage(p.Input);
            if (!image) return;

            if (p.Started)
            {
                if (_blockEnforcers.TryGetValue(p.Input, out var running) && running != null)
                    StopCoroutine(running);
                _blockEnforcers[p.Input] = StartCoroutine(EnforceBlockedHighlight(image, p.Input));
            }
            else if (p.Ended)
            {
                if (_blockEnforcers.TryGetValue(p.Input, out var running) && running != null)
                    StopCoroutine(running);
                _blockEnforcers.Remove(p.Input);

                image.color   = Color.white;
                image.enabled = false;
            }
        }

        private IEnumerator EnforceBlockedHighlight(Image img, InputEvents input)
        {
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

        // ---------- Weapon mode (unchanged) ----------

        private void HandleStationaryModeChanged(bool isStationary)
        {
            if (!view || !view.weaponModeIcon || view.weaponModeIcons == null || view.weaponModeIcons.Length < 2)
                return;

            int idx = isStationary ? 1 : 0;
            var sprite = view.weaponModeIcons[idx];
            if (!sprite) return;
            view.weaponModeIcon.sprite  = sprite;
            view.weaponModeIcon.enabled = true;
        }

        // ---------- Heat event handling (unchanged) ----------

        void OnHeatBuildStarted()   => StartHeatFillLoop();
        void OnOverheated()         => StartHeatFillLoop();
        void OnHeatDecayStarted()   => StartHeatFillLoop();

        void OnHeatDecayCompleted()
        {
            StopHeatFillLoop();
            ApplyBoostVisual(overheatingExecutor ? overheatingExecutor.Heat01 : 0f, false);
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
            if (!img || !overheatingExecutor) yield break;

            while (true)
            {
                float heat = Mathf.Clamp01(overheatingExecutor.Heat01);
                bool  hot  = overheatingExecutor.IsOverheating;
                ApplyBoostVisual(heat, hot);
                yield return new WaitForSeconds(0.05f);
            }
        }

        void ApplyBoostVisual(float shown01, bool overheated)
        {
            if (!view?.boostFill) return;

            view.boostFill.fillAmount = Mathf.Clamp01(shown01);
            view.boostFill.color = overheated
                ? overheatingColor
                : Mathf.Approximately(shown01, 1f) ? boostFullColor : boostNormalColor;
        }

        // ---------- Ammo HUD (event-driven) ----------

        private void HandleAmmoChanged(float ammo01)
        {
            PaintMissilesFromAmmo01(ammo01);
        }

        private void PaintMissilesFromAmmo01(float ammo01)
        {
            if (!view || !view.missileIcon || view.missileIcons == null || view.missileIcons.Length == 0) 
                return;

            int maxState = view.missileIcons.Length - 1;
            int state = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Clamp01(ammo01) * maxState),
                0, maxState);

            var sprite = view.missileIcons[state];
            if (sprite)
                view.missileIcon.sprite = sprite;
            view.missileIcon.enabled = true;
        }
    }
}
