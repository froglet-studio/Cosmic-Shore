using UnityEngine;
using CosmicShore.Game;

[CreateAssetMenu(fileName="SkimmerOverchargeCountHudSub", menuName="HUD/Subs/Skimmer Overcharge Count")]
public class SkimmerOverchargeHudSub : HudSubscriptionSO
{
    [Header("Channel")]
    [SerializeField] private OverchargeEventSO channel;

    [Header("HUD Text Key (ShipHUDEffects → Texts)")]
    [SerializeField] private string textKey = "SkimmerOverchargeText";

    // Optional: also mirror a bar (normalized)
    [SerializeField] private bool driveMeter = false;
    [SerializeField] private int  meterIndex = 0;

    private System.Action<IShipStatus, int, int> _handler;

    protected override void OnEnableSubscriptions()
    {
        if (Effects == null || channel == null || ShipStatus == null) return;

        if (channel.TryGetLatest(ShipStatus, out var cur, out var max))
        {
            Effects.SetText(textKey, $"{cur}/{max}");
            if (driveMeter) Effects.SetMeter(meterIndex, max > 0 ? (float)cur / max : 0f);
        }

        _handler = (who, current, max) =>
        {
            if (who != ShipStatus) return;
            Effects.SetText(textKey, $"{current}/{max}");
            if (driveMeter) Effects.SetMeter(meterIndex, max > 0 ? (float)current / max : 0f);
        };

        channel.OnRaised += _handler;
    }

    protected override void OnDisableSubscriptions()
    {
        if (channel != null && _handler != null)
            channel.OnRaised -= _handler;
        _handler = null;
    }
}