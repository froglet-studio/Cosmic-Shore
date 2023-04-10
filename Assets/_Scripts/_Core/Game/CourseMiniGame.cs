using UnityEngine;
public class CourseMiniGame : MiniGame
{
    [SerializeField] Crystal Crystal;
    [SerializeField] Vector3 CrystalStartPosition;
    [SerializeField] SegmentSpawner SegmentSpawner;


    public static new ShipTypes PlayerShipType = ShipTypes.Shark;

    protected override void Start()
    {
        base.Start();

        gameMode = MiniGames.Rampage;
        SegmentSpawner.Seed = new System.Random().Next();
    }

    protected override void SetupTurn()
    {
        base.SetupTurn();

        SegmentSpawner.numberOfSegments = 20;
        
        TrailSpawner.NukeTheTrails();
        Crystal.transform.position = CrystalStartPosition;

        SegmentSpawner.Initialize(DifficultyLevel);
        SegmentSpawner.StraightLineLength = 360 / DifficultyLevel;
    }
}