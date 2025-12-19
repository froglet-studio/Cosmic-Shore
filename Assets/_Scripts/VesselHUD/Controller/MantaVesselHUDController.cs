using CosmicShore.Game.UI.Toast;
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

        [Header("UI Toasts")]
        [SerializeField] private ToastChannel toastChannel;

        int _max = 1;
        readonly Color _overchargeTextColor = Color.red;

        public override void Initialize(IVesselStatus vesselStatus, VesselHUDView baseView)
        {
            if (vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser) return;
            
            base.Initialize(vesselStatus, baseView);

            if (!view)
                view = baseView as MantaVesselHUDView;

            if (overchargeSO == null || skimmer == null || view == null)
            {
                Debug.LogWarning("[MantaHUD] Missing references.");
                return;
            }

            _max = Mathf.Max(1, overchargeSO.MaxBlockHits);
            view.InitializeOvercharge(_max);

            overchargeSO.OnCountChanged      += HandleCountChanged;
            overchargeSO.OnReadyToOvercharge += HandleReadyToOvercharge;
            overchargeSO.OnOvercharge        += HandleOvercharge;
            overchargeSO.OnCooldownStarted   += HandleCooldownStarted;
        }

        void OnDestroy()
        {
            if (overchargeSO == null) return;
            overchargeSO.OnCountChanged      -= HandleCountChanged;
            overchargeSO.OnReadyToOvercharge -= HandleReadyToOvercharge;
            overchargeSO.OnOvercharge        -= HandleOvercharge;
            overchargeSO.OnCooldownStarted   -= HandleCooldownStarted;
        }

        void HandleCountChanged(SkimmerImpactor who, int count, int max)
        {
            if (who != skimmer || view == null) 
                return;

            _max = Mathf.Max(1, max);
            view.SetOverchargeCount(count, _max);
        }

        void HandleReadyToOvercharge(SkimmerImpactor who)
        {
            if (who != skimmer || view == null) 
                return;

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
            if (who != skimmer) 
                return;

            toastChannel?.ShowPrefix(
                prefix: "OVERCHARGED!",
                duration: 4.5f,
                accent: _overchargeTextColor
            );
        }

        void HandleCooldownStarted(SkimmerImpactor who, float seconds)
        {
            if (who != skimmer || view == null) 
                return;

            view.ResetOvercharge(_max);
        }
    }
}
