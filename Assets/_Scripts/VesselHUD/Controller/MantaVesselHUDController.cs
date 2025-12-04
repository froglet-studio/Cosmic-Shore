using CosmicShore.Game.UI.Toast;
using UnityEngine;

namespace CosmicShore.Game
{
    public class MantaVesselHUDController : VesselHUDController
    {
        [Header("View")] [SerializeField] private MantaVesselHUDView view;

        [Header("Effect Source (SO)")] [SerializeField]
        private SkimmerOverchargeCollectPrismEffectSO overchargeSO;

        [Header("Skimmer binding")] [SerializeField]
        private SkimmerImpactor skimmer;

        [Header("UI Toasts")] [SerializeField] private ToastChannel toastChannel;

        private int _max = 1;
        private Coroutine _countdownCR;
        private readonly Color _overchargeTextColor = Color.red;

        public override void Initialize(IVesselStatus vesselStatus, VesselHUDView baseView)
        {
            base.Initialize(vesselStatus, baseView);
            view = view != null ? view : baseView as MantaVesselHUDView;

            if (overchargeSO == null || skimmer == null || view == null)
            {
                Debug.LogWarning("[MantaHUD] Missing references.");
                return;
            }

            overchargeSO.OnCountChanged += HandleCountChanged;
            overchargeSO.OnReadyToOvercharge += HandleReadyToOvercharge;
            overchargeSO.OnOvercharge += HandleOvercharge;
            overchargeSO.OnCooldownStarted += HandleCooldownStarted;

            view.FillImage.fillAmount = 0f;
            view.FillImage.color = view.NormalColor;
            if (view.OverchargeCountdownContainer) view.OverchargeCountdownContainer.SetActive(false);
            SetCounter(0, overchargeSO.MaxBlockHits);
        }

        private void OnDestroy()
        {
            if (overchargeSO != null)
            {
                overchargeSO.OnCountChanged -= HandleCountChanged;
                overchargeSO.OnReadyToOvercharge -= HandleReadyToOvercharge;
                overchargeSO.OnOvercharge -= HandleOvercharge;
                overchargeSO.OnCooldownStarted -= HandleCooldownStarted;
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
            view.FillImage.color = (count >= _max) ? view.HighLightColor : view.NormalColor;

            // toastChannel?.Raise(
            //     new ToastRequest(
            //         message: $"Overcharge {count}/{_max}",
            //         duration: 1.2f,
            //         animation: ToastAnimation.SlideFromRight
            //     )
            // );
        }

        void HandleReadyToOvercharge(SkimmerImpactor who)
        {
            if (who != skimmer) return;

            toastChannel?.ShowCountdown(
                prefix: "Overcharging in",
                from: 3,
                postfixFormat: "{0}",
                anim: ToastAnimation.Pop,
                onDone: () => overchargeSO.ConfirmOvercharge(who),
                accent: view.HighLightColor
            );
        }

        void HandleOvercharge(SkimmerImpactor who)
        {
            if (who != skimmer) return;
            toastChannel.ShowPrefix(prefix:"OVERCHARGED!", duration:4.5f,accent : _overchargeTextColor);
        }

        void HandleCooldownStarted(SkimmerImpactor who, float seconds)
        {
            if (who != skimmer) return;

            SetCounter(0, _max);
            view.FillImage.fillAmount = 0f;
            view.FillImage.color = view.NormalColor;
            if (view.OverchargeCountdownContainer) view.OverchargeCountdownContainer.SetActive(false);
        }

        void SetCounter(int count, int max)
        {
            if (view?.OverchargePrismCount)
                view.OverchargePrismCount.text = $"{count}";
        }
    }
}