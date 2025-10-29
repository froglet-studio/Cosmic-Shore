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
        [SerializeField] private ConsumeBoostActionExecutor consumeBoostExecutor;

        [Header("Shields (resource-driven)")]
        [SerializeField] private int shieldResourceIndex = 0;

        [Header("Boost pip colors")]
        [SerializeField] private Color pipFull      = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color pipConsuming = new Color(0.3f, 1f, 0.3f);
        [SerializeField] private Color pipEmpty     = new Color(1f, 1f, 1f, 0.25f);

        private IVesselStatus _status;
        private ResourceSystem _rs;

        private Coroutine[] _pipAnim;          // drain/refill coroutines for consumption or bulk fills
        private bool[] _pipReloadingLive;      // true while per-pip reload is in progress (driven by Progress events)

        public override void Initialize(IVesselStatus vesselStatus, VesselHUDView baseView)
        {
            base.Initialize(vesselStatus, baseView);
            _status = vesselStatus;
            view = view != null ? view : baseView as SerpentVesselHUDView;

            // --- Ensure ALL HUD visuals are enabled at start ---
            if (view != null)
            {
                if (!view.gameObject.activeSelf) view.gameObject.SetActive(true);

                // Shield icon on
                if (view.shieldIcon != null)
                {
                    view.shieldIcon.enabled = true;
                    var go = view.shieldIcon.gameObject;
                    if (go && !go.activeSelf) go.SetActive(true);
                }

                var pips = view.BoostPips;
                if (pips != null && pips.Length > 0)
                {
                    _pipAnim = new Coroutine[pips.Length];
                    _pipReloadingLive = new bool[pips.Length];
                
                    for (int i = 0; i < pips.Length; i++)
                    {
                        var pip = pips[i];
                        if (!pip) continue;
                         pip.gameObject.SetActive(true);
                        pip.enabled = true;
                        pip.type = Image.Type.Filled;
                        pip.fillAmount = 1f;
                        pip.color = pipFull;
                    }
                }
            }

            if (consumeBoostExecutor == null)
            {
                var registry = vesselStatus?.ShipTransform
                    ? vesselStatus.ShipTransform.GetComponentInChildren<ActionExecutorRegistry>(true)
                    : null;
                if (registry != null)
                    consumeBoostExecutor = registry.Get<ConsumeBoostActionExecutor>();
            }

            // Resources
            _rs = _status?.ResourceSystem;
            if (_rs != null) _rs.OnResourceChanged += HandleResourceChanged;

            // Subscribe to boost executor events
            if (consumeBoostExecutor != null)
            {
                consumeBoostExecutor.OnChargesSnapshot += HandleBoostSnapshot;        // sets full/empty states
                consumeBoostExecutor.OnChargeConsumed  += HandleBoostChargeConsumed;  // animates a pip 1->0 (duration)
                // Per-pip reload sequence
                consumeBoostExecutor.OnReloadPipStarted   += HandleReloadPipStarted;
                consumeBoostExecutor.OnReloadPipProgress  += HandleReloadPipProgress;
                consumeBoostExecutor.OnReloadPipCompleted += HandleReloadPipCompleted;

                // Optional legacy hooks (no-ops here, but safe to keep)
                // consumeBoostExecutor.OnReloadStarted    += HandleBoostReloadStarted; // not used with per-pip events

                // Force initial snapshot so HUD matches current executor state
                HandleBoostSnapshot(
                    consumeBoostExecutor.AvailableCharges,
                    consumeBoostExecutor.MaxCharges
                );
            }

            // Shields initial paint
            PushInitialShields();
        }

        private void OnDestroy()
        {
            if (_rs != null) _rs.OnResourceChanged -= HandleResourceChanged;

            if (consumeBoostExecutor != null)
            {
                consumeBoostExecutor.OnChargesSnapshot     -= HandleBoostSnapshot;
                consumeBoostExecutor.OnChargeConsumed      -= HandleBoostChargeConsumed;

                consumeBoostExecutor.OnReloadPipStarted    -= HandleReloadPipStarted;
                consumeBoostExecutor.OnReloadPipProgress   -= HandleReloadPipProgress;
                consumeBoostExecutor.OnReloadPipCompleted  -= HandleReloadPipCompleted;
            }

            if (_pipAnim != null)
            {
                for (int i = 0; i < _pipAnim.Length; i++)
                {
                    if (_pipAnim[i] != null) StopCoroutine(_pipAnim[i]);
                    _pipAnim[i] = null;
                }
            }
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
            if (!view?.shieldIcon || view.shieldIconsByCount == null || view.shieldIconsByCount.Length < 5) return;

            shields = Mathf.Clamp(shields, 0, 4);
            var sprite = view.shieldIconsByCount[shields];
            if (!sprite) return;

            // Ensure icon is visible
            if (!view.shieldIcon.gameObject.activeSelf) view.shieldIcon.gameObject.SetActive(true);
            view.shieldIcon.enabled = true;
            view.shieldIcon.sprite = sprite;
        }

        // ------------ Boost pips (charges) ------------

        // Full snapshot: set exactly how many are full (left→right)
        void HandleBoostSnapshot(int available, int max)
        {
            if (view == null || view.BoostPips == null) return;

            var pips = view.BoostPips;
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = pips[i];
                if (!pip) continue;

                // Make sure visuals are on
                if (!pip.gameObject.activeSelf) pip.gameObject.SetActive(true);
                pip.enabled = true;

                // If pip is animating or live-reloading, don't stomp it
                if (_pipAnim != null && i < _pipAnim.Length && _pipAnim[i] != null) continue;
                if (_pipReloadingLive != null && i < _pipReloadingLive.Length && _pipReloadingLive[i]) continue;

                bool full = i < available;
                if (pip.type != Image.Type.Filled) pip.type = Image.Type.Filled;
                pip.fillAmount = full ? 1f : 0f;
                pip.color = full ? pipFull : pipEmpty;
            }
        }

        // Consume: animate rightmost full pip 1 → 0 during boostDuration
        void HandleBoostChargeConsumed(int pipIndex, float duration)
        {
            if (view == null || view.BoostPips == null) return;

            var pips = view.BoostPips;
            if (pipIndex < 0 || pipIndex >= pips.Length) return;

            // If this pip was in live reload mode, stop it first
            CancelLiveReloadIfAny(pipIndex);

            var pip = pips[pipIndex];
            float from = pip.fillAmount > 0f ? pip.fillAmount : 1f;
            StartPipAnim(pipIndex, from, 0f, Mathf.Max(0.05f, duration), pipConsuming);
        }

        // NEW: Per-pip reload events (sequential fill)
        void HandleReloadPipStarted(int pipIndex, float seconds)
        {
            if (view == null || view.BoostPips == null) return;
            var pips = view.BoostPips;
            if (pipIndex < 0 || pipIndex >= pips.Length) return;

            // Stop any existing drain/fill animation on this pip
            if (_pipAnim != null && _pipAnim[pipIndex] != null)
            {
                StopCoroutine(_pipAnim[pipIndex]);
                _pipAnim[pipIndex] = null;
            }

            _pipReloadingLive[pipIndex] = true;

            var pip = pips[pipIndex];
            if (!pip.gameObject.activeSelf) pip.gameObject.SetActive(true);
            pip.enabled = true;
            if (pip.type != Image.Type.Filled) pip.type = Image.Type.Filled;

            // Start from current (likely 0), color to "filling"
            pip.color = pipFull; // looks good since it's refilling to full
            // We'll drive fillAmount via Progress events to stay in sync with executor timers.
        }

        void HandleReloadPipProgress(int pipIndex, float norm)
        {
            if (view == null || view.BoostPips == null) return;
            var pips = view.BoostPips;
            if (pipIndex < 0 || pipIndex >= pips.Length) return;

            if (!_pipReloadingLive[pipIndex]) return;

            var pip = pips[pipIndex];
            pip.enabled = true;
            if (pip.type != Image.Type.Filled) pip.type = Image.Type.Filled;
            pip.fillAmount = Mathf.Clamp01(norm);
            // keep color as full while rising
        }

        void HandleReloadPipCompleted(int pipIndex)
        {
            if (view == null || view.BoostPips == null) return;
            var pips = view.BoostPips;
            if (pipIndex < 0 || pipIndex >= pips.Length) return;

            _pipReloadingLive[pipIndex] = false;

            var pip = pips[pipIndex];
            pip.enabled = true;
            if (pip.type != Image.Type.Filled) pip.type = Image.Type.Filled;
            pip.fillAmount = 1f;
            pip.color = pipFull;
        }

        // (Legacy global reload — kept for compatibility if you still emit it somewhere)
        // Here we do nothing because per-pip events are authoritative.
        void HandleBoostReloadStarted(float fillTime) { /* intentionally empty with per-pip events */ }

        // ------------ Pip animation helpers ------------

        void StartPipAnim(int i, float from, float to, float seconds, Color colorDuring)
        {
            if (view == null || view.BoostPips == null) return;
            var pip = view.BoostPips[i];
            if (!pip) return;

            // If a live reload is active for this pip, cancel it (consumption overrides)
            CancelLiveReloadIfAny(i);

            if (_pipAnim[i] != null) StopCoroutine(_pipAnim[i]);
            _pipAnim[i] = StartCoroutine(CoAnimatePip(i, pip, from, to, seconds, colorDuring));
        }

        IEnumerator CoAnimatePip(int i, Image pip, float from, float to, float seconds, Color colorDuring)
        {
            pip.enabled = true;
            if (!pip.gameObject.activeSelf) pip.gameObject.SetActive(true);
            if (pip.type != Image.Type.Filled) pip.type = Image.Type.Filled;

            float start = Mathf.Clamp01(from);
            float end   = Mathf.Clamp01(to);

            pip.color = colorDuring;
            pip.fillAmount = start;

            float t = 0f;
            while (t < seconds)
            {
                // If executor began a live reload on this pip mid-animation, abort anim and yield control
                if (_pipReloadingLive != null && _pipReloadingLive[i])
                {
                    _pipAnim[i] = null;
                    yield break;
                }

                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                pip.fillAmount = Mathf.Lerp(start, end, k);
                yield return null;
            }

            pip.fillAmount = end;
            pip.color = (end >= 1f) ? pipFull : pipEmpty;

            _pipAnim[i] = null;
        }

        void CancelLiveReloadIfAny(int pipIndex)
        {
            if (_pipReloadingLive != null && pipIndex >= 0 && pipIndex < _pipReloadingLive.Length)
                _pipReloadingLive[pipIndex] = false;
        }
    }
}
