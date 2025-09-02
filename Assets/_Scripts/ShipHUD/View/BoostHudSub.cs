using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "BoostHudSub", menuName = "HUD/Subs/Boost")]
    public class BoostHudSub : HudSubscriptionSO
    {
        [SerializeField] private ConsumeBoostAction action;
        protected override void OnEnableSubscriptions()
        {
            if (!action || Effects == null) return;
            // action.OnBoostStarted += HandleStart;
            // action.OnBoostEnded   += HandleEnd;
        }
        protected override void OnDisableSubscriptions()
        {
            if (!action) return;
            // action.OnBoostStarted -= HandleStart;
            // action.OnBoostEnded   -= HandleEnd;
        }

        // void HandleStart(float duration, float fromNorm) => Effects.AnimateDrain(action.ResourceIndex, duration, fromNorm);
        // void HandleEnd() => Effects.AnimateRefill(action.ResourceIndex, action.BoostDuration, 1f);
    }
}