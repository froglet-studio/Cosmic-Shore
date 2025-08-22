using UnityEngine;
using CosmicShore.Game;
using CosmicShore.Core;

[CreateAssetMenu(fileName="OverheatHudSub", menuName="HUD/Subs/Overheat")]
public class OverheatHudSub : HudSubscriptionSO
{
    [Header("HUD Meter Slot (ShipHUDEffects → Meters)")]
    [SerializeField] private int meterIndex = 0;

    // cached
    private OverheatingAction _action;
    private ResourceSystem _rs;
    private Resource _heat;
    private Resource.ResourceUpdateDelegate _onHeatChanged;

    protected override void OnEnableSubscriptions()
    {
        _action = Refs ? Refs.overheating : null;
        if (_action == null || Effects == null) return;

        var host = ShipStatus as MonoBehaviour;
        _rs = host ? host.GetComponent<ResourceSystem>() : null;
        if (_rs == null) return;

        int idx = _action.HeatResourceIndex;
        if (idx < 0 || idx >= _rs.Resources.Count) return;

        _heat = _rs.Resources[idx];

        // Initial push
        Push(_heat.CurrentAmount, _heat.MaxAmount);
        _onHeatChanged = OnHeatChanged;
        _heat.OnResourceChange += _onHeatChanged;
    }

    protected override void OnDisableSubscriptions()
    {
        if (_heat != null && _onHeatChanged != null)
            _heat.OnResourceChange -= _onHeatChanged;

        _onHeatChanged = null;
        _heat = null;
        _rs = null;
        _action = null;
    }

    private void OnHeatChanged(float current)
    {
        if (_heat == null || Effects == null) return;
        Push(current, _heat.MaxAmount);
    }

    private void Push(float current, float max)
    {
        float norm = (max > 0f) ? Mathf.Clamp01(current / max) : 0f;
        Effects.SetMeter(meterIndex, norm);   // mirrors the resource exactly
    }
}