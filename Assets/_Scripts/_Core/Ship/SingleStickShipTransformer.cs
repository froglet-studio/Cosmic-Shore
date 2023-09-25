using UnityEngine;
using StarWriter.Core;
using System.Collections.Generic;
using System.Collections;

public class SingleStickShipTransformer : ShipTransformer
{
    [SerializeField] Gun topGun;

    public float ProjectileScale = 1f;
    public Vector3 BlockScale = new(4f, 4f, 1f);

    List<Gun> guns;

    protected override void Start()
    {
        base.Start();
        inputController.SingleStickControls = true;
        guns = new List<Gun>() { topGun};
        foreach (var gun in guns)
        {
            gun.Team = ship.Team;
            gun.Ship = ship;
        }
        StartCoroutine(InitializeSingleStickControlsCoroutine());
    }

    IEnumerator InitializeSingleStickControlsCoroutine()
    {
        yield return new WaitUntil(() => inputController != null);
        inputController.SingleStickControls = true;
    }

}