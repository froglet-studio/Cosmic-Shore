using CosmicShore;
using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName="FullAutoHudSub", menuName="HUD/Subs/FullAuto")]
public class FullAutoHudSub : HudSubscriptionSO
{
    [SerializeField] private FullAutoAction action;
    [SerializeField] private string key = "FullAutoIcon";
    protected override void OnEnableSubscriptions()
    {
        if (!action || Effects == null) return;
        action.OnFullAutoStarted += () => Effects.SetToggle(key, true);
        action.OnFullAutoStopped += () => Effects.SetToggle(key, false);
    }
    protected override void OnDisableSubscriptions()
    {
        if (!action) return;
        action.OnFullAutoStarted -= actionOnOnFullAutoStarted;
        action.OnFullAutoStopped -= actionOnOnFullAutoStopped;
    }

    private void actionOnOnFullAutoStopped()
    {
        Effects.SetToggle(key, false);
    }

    private void actionOnOnFullAutoStarted()
    {
        Effects.SetToggle(key, true);
    }
}