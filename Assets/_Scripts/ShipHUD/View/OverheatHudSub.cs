using CosmicShore.Game;
using UnityEngine;

[CreateAssetMenu(fileName="OverheatHudSub", menuName="HUD/Subs/Overheat")]
public class OverheatHudSub : HudSubscriptionSO
{
    [SerializeField] private OverheatingAction action;
    [SerializeField] private string heatWarnKey = "HeatWarning";
    [SerializeField] private string overheatedTrigger = "Overheated";

    protected override void OnEnableSubscriptions()
    {
        if (!action || Effects == null) return;
        action.OnHeatBuildStarted += () => Effects.SetToggle(heatWarnKey, true);
        // action.OnOverheated += () => Effects.TriggerAnim(overheatedTrigger);
        action.OnHeatDecayCompleted += () => Effects.SetToggle(heatWarnKey, false);
    }
    protected override void OnDisableSubscriptions()
    {
        if (!action) return;
        action.OnHeatBuildStarted -= actionOnOnHeatBuildStarted;
        action.OnOverheated -= actionOnOnOverheated;
        action.OnHeatDecayCompleted -= actionOnOnHeatDecayCompleted;
    }

    private void actionOnOnHeatDecayCompleted()
    {
        Effects.SetToggle(heatWarnKey, false);
    }

    private void actionOnOnOverheated()
    {
        // Effects.TriggerAnim(overheatedTrigger);
    }

    private void actionOnOnHeatBuildStarted()
    {
        Effects.SetToggle(heatWarnKey, on: true);
    }
}