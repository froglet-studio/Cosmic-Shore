using UnityEngine;

public class DartMiniGame : MiniGame
{
    [SerializeField] SpawnableDartBoard DartBoard;
    [SerializeField] Crystal Crystal;
    [SerializeField] Vector3 CrystalStartPosition;
    [SerializeField] SegmentSpawner SegmentSpawner;

    protected override void Start()
    {
        base.Start();

        gameMode = MiniGames.Darts;

        // TODO: make these dynamic
        DartBoard.PlayerOne = Players[0];
        DartBoard.PlayerTwo = Players[1];

        SegmentSpawner.Seed = new System.Random().Next();
        SegmentSpawner.Initialize();
    }

    protected override void SetupTurn()
    {
        base.SetupTurn();

        TrailSpawner.NukeTheTrails();
        Crystal.transform.position = CrystalStartPosition;
    }
}