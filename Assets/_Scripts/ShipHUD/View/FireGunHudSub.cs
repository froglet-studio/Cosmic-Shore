using UnityEngine;
using CosmicShore.Game;

[CreateAssetMenu(fileName="FireGunHudSub", menuName="HUD/Subs/FireGun")]
public class FireGunHudSub : HudSubscriptionSO
{
    [SerializeField] private int meterIndex = 0;   // points to the SpriteSequence meter with 3 sprites
    [SerializeField] private int capacity   = 2;   // two missiles total
    [SerializeField] private bool startFull = true;

    private FireGunAction _action;
    private System.Action _onGunFired;
    private int _current;

    protected override void OnEnableSubscriptions()
    {
        _action = Refs ? Refs.fireGun : null;
        if (_action == null || Effects == null) return;

        _current = Mathf.Clamp(startFull ? capacity : _current, 0, capacity);
        Push();

        _onGunFired = () =>
        {
            if (_current > 0) _current--;
            Push();
        };
        _action.OnGunFired += _onGunFired;
    }

    protected override void OnDisableSubscriptions()
    {
        if (_action != null && _onGunFired != null)
            _action.OnGunFired -= _onGunFired;
        _onGunFired = null;
        _action = null;
    }

    private void Push()
    {
        float norm = capacity <= 0 ? 0f : (float)_current / capacity; 
        Effects.SetMeter(meterIndex, norm);
    }

    public void SetMissiles(int count)
    {
        _current = Mathf.Clamp(count, 0, capacity);
        if (Effects != null) Push();
    }
}