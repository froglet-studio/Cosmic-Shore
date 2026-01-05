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

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view)
                view = View as MantaVesselHUDView;

            if (_statusIsNotLocal(vesselStatus)) return; 

            _max = Mathf.Max(1, overchargeSO.MaxBlockHits);
            overchargeSO.OnCountChanged      += HandleCountChanged;
            overchargeSO.OnReadyToOvercharge += HandleReadyToOvercharge;
            overchargeSO.OnOvercharge        += HandleOvercharge;
            overchargeSO.OnCooldownStarted   += HandleCooldownStarted;
        }

        static bool _statusIsNotLocal(IVesselStatus s) => s.IsInitializedAsAI || !s.IsLocalUser;


        void OnDisable()
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
