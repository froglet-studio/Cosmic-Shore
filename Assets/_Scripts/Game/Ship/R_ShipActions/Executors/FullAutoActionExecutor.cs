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
        if (_loop != null || !gun) return;
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
        float dt = 1f / Mathf.Max(0.01f, so.FiringRate);
        float acc = 0f;

        while (true)
        {
            acc += Time.deltaTime;
            while (acc >= dt)
            {
                var res = _resources.Resources[so.AmmoIndex];
                if (res.CurrentAmount >= so.AmmoCost)
                {
                    var inheritVel = so.Inherit ? _status.Course * _status.Speed : Vector3.zero;

                    // fire both muzzles in the SAME tick
                    for (int i = 0; i < muzzles.Length; i++)
                        gun.FireGun(muzzles[i], so.SpeedValue.Value, inheritVel,
                            so.ProjectileScale, true, so.ProjectileTime, 0, so.FiringPattern, so.Energy);

                    _resources.ChangeResourceAmount(so.AmmoIndex, -so.AmmoCost);
                }

                acc -= dt;
            }

            yield return null;
        }
    }
}
