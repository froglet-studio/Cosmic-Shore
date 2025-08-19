using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName="FireGunHudSub", menuName="HUD/Subs/FireGun")]
public class FireGunHudSub : HudSubscriptionSO
{
    [SerializeField] private FireGunAction action;
    [SerializeField] private string triggerKey = "GunFired";
    protected override void OnEnableSubscriptions()
    {
        if (!action || Effects == null) return;
        // action.OnGunFired += () => Effects.TriggerAnim(triggerKey);
    }
    protected override void OnDisableSubscriptions()
    {
        if (!action) return;
        action.OnGunFired -= actionOnOnGunFired;
    }

    private void actionOnOnGunFired()
    {
        // Effects.TriggerAnim(triggerKey);
    }
}