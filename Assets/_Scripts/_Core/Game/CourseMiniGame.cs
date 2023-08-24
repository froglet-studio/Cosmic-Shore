using UnityEngine;
public class CourseMiniGame : MiniGame
{
    [SerializeField] Crystal Crystal;
    [SerializeField] Vector3 CrystalStartPosition;
    [SerializeField] SegmentSpawner SegmentSpawner;
    [SerializeField] int numberOfSegments = 10;
    [SerializeField] int straightLineLength = 400;


    public static new ShipTypes PlayerShipType = ShipTypes.Shark;

    protected override void Start()
    {
        base.Start();
        SegmentSpawner.Seed = new System.Random().Next();
        numberOfSegments = numberOfSegments * DifficultyLevel;
    }

    protected override void SetupTurn()
    {
        base.SetupTurn();

        SegmentSpawner.numberOfSegments = numberOfSegments;
        SegmentSpawner.StraightLineLength = straightLineLength / DifficultyLevel;

        TrailSpawner.NukeTheTrails();
        Crystal.transform.position = CrystalStartPosition;

        SegmentSpawner.Initialize();
    }
}