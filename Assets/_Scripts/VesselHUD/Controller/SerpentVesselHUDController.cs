using System.Collections;
using CosmicShore.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class SerpentVesselHUDController : VesselHUDController
    {
        [Header("View")] [SerializeField] private SerpentVesselHUDView view;

        [Header("Boost (charges)")] [SerializeField]
        private ConsumeBoostActionExecutor consumeBoostExecutor;

        [Header("Shields")] [SerializeField]
        private int shieldResourceIndex;

        [Header("Boost pip colors")] [SerializeField]
        private Color pipFull = new(1f, 1f, 1f, 1f);

        [SerializeField] private Color pipConsuming = new(0.3f, 1f, 0.3f);
        [SerializeField] private Color pipEmpty = new(1f, 1f, 1f, 0.25f);

        private IVesselStatus _status;
        private ResourceSystem _rs;

        private Coroutine[] _pipAnim;
        private bool[] _pipReloadingLive; 

        public override void Initialize(IVesselStatus vesselStatus, VesselHUDView baseView)
        {
            base.Initialize(vesselStatus, baseView);
            _status = vesselStatus;
            view = view != null ? view : baseView as SerpentVesselHUDView;

            if (view != null)
            {
                if (!view.gameObject.activeSelf) view.gameObject.SetActive(true);

                if (view.shieldIcon != null)
                {
                    view.shieldIcon.enabled = true;
                    var go = view.shieldIcon.gameObject;
                    if (go && !go.activeSelf) go.SetActive(true);
                }

                var pips = view.BoostPips;
                if (pips is { Length: > 0 })
                {
                    _pipAnim = new Coroutine[pips.Length];
                    _pipReloadingLive = new bool[pips.Length];

                    foreach (var pip in pips)
                    {
                        if (!pip) continue;
                        pip.gameObject.SetActive(true);
                        pip.enabled = true;
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

            _rs = _status?.ResourceSystem;
            if (_rs != null) _rs.OnResourceChanged += HandleResourceChanged;

            if (consumeBoostExecutor != null)
            {

                consumeBoostExecutor.OnChargesSnapshot += HandleBoostSnapshot;
                consumeBoostExecutor.OnChargeConsumed += HandleBoostChargeConsumed;

                HandleBoostSnapshot(
                    consumeBoostExecutor.AvailableCharges,
                    consumeBoostExecutor.MaxCharges
                );
            }

            PushInitialShields();
        }

        private void OnDestroy()
        {
            if (_rs != null) _rs.OnResourceChanged -= HandleResourceChanged;

            if (consumeBoostExecutor != null)
            {
                consumeBoostExecutor.OnChargesSnapshot -= HandleBoostSnapshot;
                consumeBoostExecutor.OnChargeConsumed -= HandleBoostChargeConsumed;
            }

            if (_pipAnim == null) return;
            for (var i = 0; i < _pipAnim.Length; i++)
            {
                if (_pipAnim[i] != null) StopCoroutine(_pipAnim[i]);
                _pipAnim[i] = null;
            }
        }

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

            if (!view.shieldIcon.gameObject.activeSelf) view.shieldIcon.gameObject.SetActive(true);
            view.shieldIcon.enabled = true;
            view.shieldIcon.sprite = sprite;
        }

        void HandleBoostSnapshot(int available, int max)
        {
            if (!view || view.BoostPips == null) return;

            var pips = view.BoostPips;
            if (available >= max && pips.Length > 0)
            {
                if (_pipAnim != null)
                {
                    for (int i = 0; i < _pipAnim.Length; i++)
                    {
                        if (_pipAnim[i] != null) StopCoroutine(_pipAnim[i]);
                        _pipAnim[i] = null;
                    }
                }
                if (_pipReloadingLive != null)
                {
                    for (int i = 0; i < _pipReloadingLive.Length; i++)
                        _pipReloadingLive[i] = false;
                }

                for (int i = 0; i < pips.Length; i++)
                {
                    var pip = pips[i];
                    if (!pip) continue;

                    if (!pip.gameObject.activeSelf) pip.gameObject.SetActive(true);
                    pip.enabled = true;
                    if (pip.type != Image.Type.Filled) pip.type = Image.Type.Filled;
                    pip.fillAmount = 1f;
                    pip.color = pipFull;
                }

                return;
            }

            for (int i = 0; i < pips.Length; i++)
            {
                var pip = pips[i];
                if (!pip) continue;

                if (!pip.gameObject.activeSelf) pip.gameObject.SetActive(true);
                pip.enabled = true;

                if (_pipAnim != null && i < _pipAnim.Length && _pipAnim[i] != null) continue;
                if (_pipReloadingLive != null && i < _pipReloadingLive.Length && _pipReloadingLive[i]) continue;

                bool full = i < available;
                if (pip.type != Image.Type.Filled) pip.type = Image.Type.Filled;
                pip.fillAmount = full ? 1f : 0f;
                pip.color = full ? pipFull : pipEmpty;
            }
        }


        void HandleBoostChargeConsumed(int pipIndex, float duration)
        {
            if (!view || view.BoostPips == null) return;

            var pips = view.BoostPips;
            if (pipIndex < 0 || pipIndex >= pips.Length) return;

            CancelLiveReloadIfAny(pipIndex);

            var pip = pips[pipIndex];
            float from = pip.fillAmount > 0f ? pip.fillAmount : 1f;
            StartPipAnim(pipIndex, from, 0f, Mathf.Max(0.05f, duration), pipConsuming);
        }

        void StartPipAnim(int i, float from, float to, float seconds, Color colorDuring)
        {
            if (!view || view.BoostPips == null) return;
            var pip = view.BoostPips[i];
            if (!pip) return;

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
            float end = Mathf.Clamp01(to);

            pip.color = colorDuring;
            pip.fillAmount = start;

            float t = 0f;
            while (t < seconds)
            {
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