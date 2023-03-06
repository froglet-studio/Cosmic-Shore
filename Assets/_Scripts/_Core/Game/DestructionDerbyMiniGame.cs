using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestructionDerbyMiniGame : MiniGame
{
    [SerializeField] Crystal Crystal;
    [SerializeField] Vector3 CrystalStartPosition;
    
    protected override void Start()
    {
        base.Start();


    }

    protected override void SetupTurn()
    {
        base.SetupTurn();

        TrailSpawner.NukeTheTrails();
        Crystal.transform.position = CrystalStartPosition;
    }
}
