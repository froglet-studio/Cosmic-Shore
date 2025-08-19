using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "SeedHudSub", menuName = "HUD/Subs/SeedAssembler")]
    public class SeedHudSub : HudSubscriptionSO
    {
        [SerializeField] private SeedAssemblerAction action;
        [SerializeField] private string toggleKey = "SeedAssembling";

        protected override void OnEnableSubscriptions()
        {
            if (!action || Effects == null) return;
            action.OnAssembleStarted   += OnStart;
            action.OnAssembleCompleted += OnEnd;
        }

        protected override void OnDisableSubscriptions()
        {
            if (!action) return;
            action.OnAssembleStarted   -= OnStart;
            action.OnAssembleCompleted -= OnEnd;
        }

        void OnStart() => Effects.SetToggle(toggleKey, true);
        void OnEnd()   => Effects.SetToggle(toggleKey, false);
    }
}