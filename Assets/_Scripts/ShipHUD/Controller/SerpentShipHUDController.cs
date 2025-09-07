using System.Collections;
using CosmicShore.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class SerpentShipHUDController : ShipHUDController
    {
        [Header("View")]
        [SerializeField] private SerpentShipHUDView view;

        [Header("Boost (charges)")]
        [SerializeField] private ConsumeBoostAction consumeBoost;

        [Header("Shields (resource-driven)")]
        [SerializeField] private int shieldResourceIndex = 0;

        [Header("Boost pip colors")]
        [SerializeField] private Color pipFull      = Color.white;
        [SerializeField] private Color pipConsuming = new Color(0.3f, 1f, 0.3f);
        [SerializeField] private Color pipEmpty     = new Color(1f, 1f, 1f, 0.25f);

        private IShipStatus _status;
        private ResourceSystem _rs;

        private Coroutine[] _pipAnim;

        public override void Initialize(IShipStatus shipStatus, ShipHUDView baseView)
        {
            base.Initialize(shipStatus, baseView);
            _status = shipStatus;
            view = view != null ? view : baseView as SerpentShipHUDView;
            if(view != null && !view.isActiveAndEnabled) view.gameObject.SetActive(true);
            // shields: subscribe to resource change
            _rs = _status.ResourceSystem;
            if (_rs != null) _rs.OnResourceChanged += HandleResourceChanged;

            // pips setup: ensure visible, filled type, and start full
            var pips = view.BoostPips;
            _pipAnim = new Coroutine[pips.Length];
            for (int i = 0; i < pips.Length; i++)
            {
                var pip = pips[i];
                if (!pip) continue;
                pip.enabled = true;                        // ensure visible (fixes #1)
                if (pip.type != Image.Type.Filled) pip.type = Image.Type.Filled;
                pip.fillAmount = 1f;                       // start full
                pip.color = pipFull;
            }

            // subscribe to magazine events
            if (consumeBoost != null)
            {
                consumeBoost.OnChargesSnapshot += HandleBoostSnapshot;      // set all pips full/empty
                consumeBoost.OnChargeConsumed  += HandleBoostChargeConsumed;// animate 1→0
                consumeBoost.OnReloadStarted   += HandleBoostReloadStarted; // animate ALL 0→1
            }

            // shields initial paint
            PushInitialShields();
        }

        private void OnDestroy()
        {
            if (_rs != null) _rs.OnResourceChanged -= HandleResourceChanged;

            if (consumeBoost != null)
            {
                consumeBoost.OnChargesSnapshot -= HandleBoostSnapshot;
                consumeBoost.OnChargeConsumed  -= HandleBoostChargeConsumed;
                consumeBoost.OnReloadStarted   -= HandleBoostReloadStarted;
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

                // stop any anim so snapshot wins
                if (_pipAnim[i] != null) { StopCoroutine(_pipAnim[i]); _pipAnim[i] = null; }

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
