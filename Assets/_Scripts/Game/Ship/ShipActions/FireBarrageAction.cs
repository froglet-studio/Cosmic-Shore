using System.Collections.Generic;
using CosmicShore.Game.Projectiles;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;

public class FireBarrageAction : ShipAction
{
    // TODO: WIP gun firing needs to be reworked
    [SerializeField] Gun gunContainer;
    List<Gun> guns = new();
    [SerializeField] PoolManager projectileContainer;

    [SerializeField] int ammoIndex = 0;
    [SerializeField] float ammoCost = .03f;
    
    bool inherit = false;

    float ProjectileScale = 1f;

    public FiringPatterns FiringPattern = FiringPatterns.Default;
    public int Energy = 0;
    public float speed = 7;
    public float projectileTime = 3;

    void CopyValues<T>(T from, T to)
    {
        var json = JsonUtility.ToJson(from);
        JsonUtility.FromJsonOverwrite(json, to);
    }

    public override void Initialize(IVessel vessel)
    {
        base.Initialize(vessel);
        var gunTemplate = gunContainer.GetComponent<Gun>();
        foreach (var child in gunContainer.GetComponentsInChildren<Transform>()) 
        {
            var go = child.gameObject;
            CopyValues<Gun>(gunTemplate, go.AddComponent<Gun>());
            guns.Add(go.GetComponent<Gun>());
            child.LookAt(gunContainer.transform);
            child.Rotate(0, 180, 0);
        }
        //projectileContainer = new GameObject($"{vessel.Player.PlayerName}_BarrageProjectiles");
    }

    public override void StartAction()
    {
        if (ResourceSystem.Resources[ammoIndex].CurrentAmount > ammoCost)
        {
            ResourceSystem.ChangeResourceAmount(ammoIndex, -ammoCost);

            Vector3 inheritedVelocity;

            if (ResourceSystem.Resources[ammoIndex].CurrentAmount > ammoCost)
            {
                // TODO: WIP magic numbers
                foreach (var gun in guns)
                {
                    if (inherit)
                    {
                        if (VesselStatus.Attached) inheritedVelocity = gun.transform.forward;
                        else inheritedVelocity = VesselStatus.Course;
                    }
                    else inheritedVelocity = Vector3.zero;
                    gun.FireGun(projectileContainer.transform, speed, inheritedVelocity * VesselStatus.Speed, ProjectileScale, true, projectileTime, 0, FiringPattern, Energy);
                }
            }
             
        }
    }

    public override void StopAction()
    {

    }
}