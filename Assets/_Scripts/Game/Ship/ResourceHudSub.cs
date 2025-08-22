using UnityEngine;
using CosmicShore.Core;   // <-- for Resource & ResourceSystem
using CosmicShore.Game;   // <-- for HudSubscriptionSO & IHUDEffects

[CreateAssetMenu(fileName="ResourceHudSub", menuName="HUD/Subs/Resource→Meter")]
public class ResourceHudSub : HudSubscriptionSO
{
    [Header("Resource & Meter")]
    [SerializeField] private int resourceIndex = 0;  // index in ResourceSystem.Resources
    [SerializeField] private int meterIndex    = 0;  // index in ShipHUDEffects.Meters (R_ResourceDisplay)

    [Header("Animation")]
    [SerializeField] private bool  animate = true;
    [SerializeField] private float seconds = 0.3f;

    private ResourceSystem _rs;
    private Resource _resource;

    // Use the exact delegate type from Resource
    private Resource.ResourceUpdateDelegate _onChange;

    protected override void OnEnableSubscriptions()
    {
        var host = ShipStatus as MonoBehaviour;
        _rs = host ? host.GetComponent<ResourceSystem>() : null;
        if (_rs == null || Effects == null) return;
        if (resourceIndex < 0 || resourceIndex >= _rs.Resources.Count) return;

        _resource = _rs.Resources[resourceIndex];

        // Cache a delegate of the correct type so we can unsubscribe later
        _onChange = OnResourceChanged;
        _resource.OnResourceChange += _onChange;
    }

    private void OnResourceChanged(float value)
    {

    }

    protected override void OnDisableSubscriptions()
    {
        if (_resource != null && _onChange != null)
            _resource.OnResourceChange -= _onChange;

        _onChange = null;
        _resource = null;
        _rs = null;
    }
}
