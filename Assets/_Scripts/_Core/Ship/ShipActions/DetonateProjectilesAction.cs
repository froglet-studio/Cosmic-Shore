using StarWriter.Core;
using UnityEngine;

public class DetonateProjectilesAction : ShipActionAbstractBase
{
    // TODO: WIP gun firing needs to be reworked
    [SerializeField] Gun gun;


    void Start()
    {

    }
    public override void StartAction()
    {
        gun.Detonate = true;
    }

    public override void StopAction()
    {
        gun.Detonate = false;
    }


}