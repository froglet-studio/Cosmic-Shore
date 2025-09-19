using System.Collections;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.Projectiles;

public sealed class FullAutoActionExecutor : ShipActionExecutorBase
{
    [Header("Scene Refs")]
    [SerializeField] Gun gun;
    [SerializeField] Transform[] muzzles;

    IVesselStatus _status;
    ResourceSystem _resources;
    Coroutine _loop;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _resources = shipStatus.ResourceSystem;
        gun?.Initialize(shipStatus);

        if ((muzzles == null || muzzles.Length == 0) && gun != null)
            muzzles = new[] { gun.transform };
    }

    public void Begin(FullAutoActionSO so)
    {
        if (_loop != null || gun == null) return;
        _loop = StartCoroutine(FireLoop(so));
    }

    public void End()
    {
        if (_loop == null) return;
        StopCoroutine(_loop);
        _loop = null;
    }

    IEnumerator FireLoop(FullAutoActionSO so)
    {
        float interval = 1f / Mathf.Max(0.01f, so.FiringRate);
        while (true)
        {
            var res = _resources.Resources[so.AmmoIndex];
            if (res.CurrentAmount >= so.AmmoCost)
            {
                var mz = (muzzles != null && muzzles.Length > 0) ? muzzles : new[] { gun.transform };
                foreach (var t in mz)
                {
                    Vector3 inheritVel = Vector3.zero;
                    if (so.Inherit)
                        inheritVel = _status.Attached ? t.forward : _status.Course;

                    gun.FireGun(
                        t,
                        so.SpeedValue.Value,
                        inheritVel * _status.Speed,
                        so.ProjectileScale,
                        true,
                        so.ProjectileTime,
                        0,
                        so.FiringPattern,
                        so.Energy);
                }
                _resources.ChangeResourceAmount(so.AmmoIndex, -so.AmmoCost);
            }
            yield return new WaitForSeconds(interval);
        }
    }
}
