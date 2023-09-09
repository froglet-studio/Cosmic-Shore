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
        gun.Detonate();
    }

    public override void StopAction()
    {
        
    }


}