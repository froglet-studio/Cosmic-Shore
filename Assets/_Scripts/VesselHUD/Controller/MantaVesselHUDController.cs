using System.Collections;
using UnityEngine;

namespace CosmicShore.Game
{
    public class MantaVesselHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private MantaVesselHUDView view;

        [Header("Effect Source (SO)")]
        [SerializeField] private SkimmerOverchargeCollectPrismEffectSO overchargeSO;

        [Header("Skimmer binding")]
        [SerializeField] private SkimmerImpactor skimmer;

        private int _max = 1;
        private Coroutine _countdownCR;

        public override void Initialize(IVesselStatus vesselStatus, VesselHUDView baseView)
        {
            base.Initialize(vesselStatus, baseView);
            view = view != null ? view : baseView as MantaVesselHUDView;

            if (overchargeSO == null || skimmer == null || view == null)
            {
                Debug.LogWarning("[MantaHUD] Missing references.");
                return;
            }

            overchargeSO.OnCountChanged        += HandleCountChanged;
            overchargeSO.OnReadyToOvercharge   += HandleReadyToOvercharge;
            overchargeSO.OnOvercharge          += HandleOvercharge;
            overchargeSO.OnCooldownStarted     += HandleCooldownStarted;

            view.FillImage.fillAmount = 0f;
            view.FillImage.color      = view.NormalColor;
            if (view.OverchargeCountdownContainer) view.OverchargeCountdownContainer.SetActive(false);
            SetCounter(0, overchargeSO.MaxBlockHits);
        }

        private void OnDestroy()
        {
            if (overchargeSO != null)
            {
                overchargeSO.OnCountChanged        -= HandleCountChanged;
                overchargeSO.OnReadyToOvercharge   -= HandleReadyToOvercharge;
                overchargeSO.OnOvercharge          -= HandleOvercharge;
                overchargeSO.OnCooldownStarted     -= HandleCooldownStarted;
            }

            if (_countdownCR != null) StopCoroutine(_countdownCR);
            _countdownCR = null;
        }

        void HandleCountChanged(SkimmerImpactor who, int count, int max)
        {
            if (who != skimmer) return;

            _max = Mathf.Max(1, max);
            SetCounter(count, _max);

            float fill = (float)count / _max;
            view.FillImage.fillAmount = fill;
            view.FillImage.color      = (count >= _max) ? view.HighLightColor : view.NormalColor;
        }

        void HandleReadyToOvercharge(SkimmerImpactor who)
        {
            if (who != skimmer) return;
            if (_countdownCR != null) StopCoroutine(_countdownCR);
            _countdownCR = StartCoroutine(CountdownAndConfirm(who));
        }

        IEnumerator CountdownAndConfirm(SkimmerImpactor who)
        {
            if (view.OverchargeCountdownContainer) view.OverchargeCountdownContainer.SetActive(true);

            for (int i = 3; i >= 1; i--)
            {
                if (view.OverChargeCountdownText) view.OverChargeCountdownText.text = i.ToString();
                yield return new WaitForSeconds(1f);
            }
            
            overchargeSO.ConfirmOvercharge(who);

            if (view.OverchargeCountdownContainer) view.OverchargeCountdownContainer.SetActive(false);

            _countdownCR = null;
        }

        void HandleOvercharge(SkimmerImpactor who)
        {
            if (who != skimmer);
        }

        void HandleCooldownStarted(SkimmerImpactor who, float seconds)
        {
            if (who != skimmer) return;

            SetCounter(0, _max);
            view.FillImage.fillAmount = 0f;
            view.FillImage.color      = view.NormalColor;
            if (view.OverchargeCountdownContainer) view.OverchargeCountdownContainer.SetActive(false);
        }

        void SetCounter(int count, int max)
        {
            if (view?.OverchargePrismCount)
                view.OverchargePrismCount.text = $"{count}";
        }
    }
}
