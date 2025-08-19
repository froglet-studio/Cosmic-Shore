using UnityEngine;
using CosmicShore.Game;

[CreateAssetMenu(fileName="OverheatHudSub", menuName="HUD/Subs/Overheat")]
public class OverheatHudSub : HudSubscriptionSO
{
    [Header("Meter slot (ShipHUDEffects → Meters array)")]
    [SerializeField] private int meterIndex = 0;

    [Header("Timings")]
    [SerializeField] private float buildSeconds   = 0.6f;  // fill duration when heat starts building
    [SerializeField] private float cooldownSeconds= 0.6f;  // drain duration when heat finishes cooling

    // cached
    private OverheatingAction _action;
    private System.Action _onBuild;
    private System.Action _onOverheated;
    private System.Action _onDecayDone;

    protected override void OnEnableSubscriptions()
    {
        _action = Refs ? Refs.overheating : null;
        if (_action == null || Effects == null) return;

        // Smooth refill to full when heat starts building
        _onBuild       = () => Effects.AnimateRefill(meterIndex, buildSeconds, 1f);
        // Ensure meter lands at full when overheated
        _onOverheated  = () => Effects.AnimateRefill(meterIndex, 0.05f, 1f);
        // Smooth drain to empty when heat finishes decaying
        _onDecayDone   = () => Effects.AnimateDrain(meterIndex, cooldownSeconds, 0f);

        _action.OnHeatBuildStarted   += _onBuild;
        _action.OnOverheated         += _onOverheated;
        _action.OnHeatDecayCompleted += _onDecayDone;
    }

    protected override void OnDisableSubscriptions()
    {
        if (_action == null) return;

        if (_onBuild      != null) _action.OnHeatBuildStarted   -= _onBuild;
        if (_onOverheated != null) _action.OnOverheated         -= _onOverheated;
        if (_onDecayDone  != null) _action.OnHeatDecayCompleted -= _onDecayDone;

        _onBuild = _onOverheated = _onDecayDone = null;
        _action = null;
    }
}