using System.Collections;
using CosmicShore.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class SerpentVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private SerpentVesselHUDView view;

        [Header("Boost (charges)")]
        // OLD: [SerializeField] private ConsumeBoostAction consumeBoost;
        [SerializeField] private ConsumeBoostActionExecutor consumeBoostExecutor; // <-- executor

        [Header("Shields (resource-driven)")]
        [SerializeField] private int shieldResourceIndex = 0;

        [Header("Boost pip colors")]
        [SerializeField] private Color pipFull      = Color.white;
        [SerializeField] private Color pipConsuming = new Color(0.3f, 1f, 0.3f);
        [SerializeField] private Color pipEmpty     = new Color(1f, 1f, 1f, 0.25f);

        private IVesselStatus _status;
        private ResourceSystem _rs;

        private Coroutine[] _pipAnim;

        public override void Initialize(IVesselStatus vesselStatus, VesselHUDView baseView)
        {
            base.Initialize(vesselStatus, baseView);
            _status = vesselStatus;
            view = view != null ? view : baseView as SerpentVesselHUDView;
            if (view != null && !view.isActiveAndEnabled) view.gameObject.SetActive(true);

            // try to auto-resolve executor from ActionExecutorRegistry if not set
            if (consumeBoostExecutor == null)
            {
                var registry = vesselStatus?.ShipTransform
                    ? vesselStatus.ShipTransform.GetComponentInChildren<ActionExecutorRegistry>(true)
                    : null;
                if (registry != null)
                    consumeBoostExecutor = registry.Get<ConsumeBoostActionExecutor>();
            }

            // shields: subscribe to resource change
            _rs = _status?.ResourceSystem;
            if (_rs != null) _rs.OnResourceChanged += HandleResourceChanged;

            // pips setup: ensure visible, filled type, and start full
            var pips = view.BoostPips;
            _pipAnim = new Coroutine[pips.Length];
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = pips[i];
                if (!pip) continue;
                pip.enabled = true;
                if (pip.type != Image.Type.Filled) pip.type = Image.Type.Filled;
                pip.fillAmount = 1f;
                pip.color = pipFull;
            }

            // subscribe to magazine events from the EXECUTOR
            if (consumeBoostExecutor != null)
            {
                consumeBoostExecutor.OnChargesSnapshot += HandleBoostSnapshot;      // set all pips full/empty
                consumeBoostExecutor.OnChargeConsumed  += HandleBoostChargeConsumed;// animate 1→0
                consumeBoostExecutor.OnReloadStarted   += HandleBoostReloadStarted; // animate ALL 0→1

                // Optional: force an initial snapshot so HUD matches current executor state
                HandleBoostSnapshot(
                    consumeBoostExecutor.AvailableCharges,
                    consumeBoostExecutor.MaxCharges
                );
            }

            // shields initial paint
            PushInitialShields();
        }

        private void OnDestroy()
        {
            if (_rs != null) _rs.OnResourceChanged -= HandleResourceChanged;

            if (consumeBoostExecutor != null)
            {
                consumeBoostExecutor.OnChargesSnapshot -= HandleBoostSnapshot;
                consumeBoostExecutor.OnChargeConsumed  -= HandleBoostChargeConsumed;
                consumeBoostExecutor.OnReloadStarted   -= HandleBoostReloadStarted;
            }

            if (_pipAnim != null)
                for (int i = 0; i < _pipAnim.Length; i++)
                    if (_pipAnim[i] != null) StopCoroutine(_pipAnim[i]);
        }

        // ------------ Shields (resource-driven) ------------

        void HandleResourceChanged(int index, float current, float max)
        {
            if (index != shieldResourceIndex || max <= 0f) return;

            float norm = Mathf.Clamp01(current / max);
            int shields = Mathf.Clamp(Mathf.FloorToInt(norm * 4f + 0.0001f), 0, 4);
            PaintShields(shields);
        }

        void PushInitialShields()
        {
            if (_rs == null) return;
            if ((uint)shieldResourceIndex >= _rs.Resources.Count) return;

            var r = _rs.Resources[shieldResourceIndex];
            float norm = (r.MaxAmount <= 0f) ? 0f : Mathf.Clamp01(r.CurrentAmount / r.MaxAmount);
            PaintShields(Mathf.Clamp(Mathf.FloorToInt(norm * 4f), 0, 4));
        }

        void PaintShields(int shields)
        {
            if (view?.shieldIcon == null || view.shieldIconsByCount == null || view.shieldIconsByCount.Length < 5) return;

            shields = Mathf.Clamp(shields, 0, 4);
            var sprite = view.shieldIconsByCount[shields];
            if (sprite != null)
            {
                view.shieldIcon.sprite = sprite;
                view.shieldIcon.enabled = true;
            }
        }

        // ------------ Boost pips (charges) ------------

        // Full snapshot: set exactly how many are full (left→right)
        void HandleBoostSnapshot(int available, int max)
        {
            var pips = view.BoostPips;
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = pips[i];
                if (!pip) continue;

                // NEW: if pip is animating (draining or refilling), DO NOT overwrite it with snapshot
                if (_pipAnim[i] != null)
                    continue; // let the ongoing animation show the true remaining time

                bool full = i < available;
                pip.enabled = true;
                pip.fillAmount = full ? 1f : 0f;
                pip.color = full ? pipFull : pipEmpty;

            }
        }

        // Consume: animate rightmost full pip 1 → 0 during boostDuration
        void HandleBoostChargeConsumed(int pipIndex, float duration)
        {
            var pips = view.BoostPips;
            if (pipIndex < 0 || pipIndex >= pips.Length) return;

            var pip = pips[pipIndex];
            float from = pip.fillAmount > 0f ? pip.fillAmount : 1f;

            StartPipAnim(pipIndex, from, 0f, Mathf.Max(0.05f, duration), pipConsuming);
        }

        // Reload (global): animate ALL pips 0 → 1 together during reloadFillTime
        void HandleBoostReloadStarted(float fillTime)
        {
            var pips = view.BoostPips;
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = pips[i]; if (!pip) continue;
                StartPipAnim(i, pip.fillAmount, 1f, Mathf.Max(0.05f, fillTime), pipFull);
            }
        }

        void StartPipAnim(int i, float from, float to, float seconds, Color colorDuring)
        {
            var pip = view.BoostPips[i];
            if (!pip) return;

            if (_pipAnim[i] != null) StopCoroutine(_pipAnim[i]);
            _pipAnim[i] = StartCoroutine(CoAnimatePip(i, pip, from, to, seconds, colorDuring));
        }

        IEnumerator CoAnimatePip(int i, Image pip, float from, float to, float seconds, Color colorDuring)
        {
            pip.enabled = true;
            if (pip.type != Image.Type.Filled) pip.type = Image.Type.Filled;

            float start = Mathf.Clamp01(from);
            float end   = Mathf.Clamp01(to);

            pip.color = colorDuring;
            pip.fillAmount = start;

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                pip.fillAmount = Mathf.Lerp(start, end, k);
                yield return null;
            }

            pip.fillAmount = end;
            pip.color = (end >= 1f) ? pipFull : pipEmpty;

            _pipAnim[i] = null;
        }
    }
}
