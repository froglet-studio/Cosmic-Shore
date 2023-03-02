using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DartMiniGame : MiniGame
{
    [SerializeField] DartBoard DartBoard;
    [SerializeField] Crystal Crystal;
    [SerializeField] Vector3 CrystalStartPosition;
    
    protected override void Start()
    {
        base.Start();

        // TODO: make these dynamic
        DartBoard.PlayerOne = Players[0];
        DartBoard.PlayerTwo = Players[1];
    }

    protected override void SetupTurn()
    {
        base.SetupTurn();

        // Setup the dartboard
        DartBoard.Initialize();
        Crystal.transform.position = CrystalStartPosition;
    }
}
