using UnityEngine;

public class CourseMiniGame : MiniGame
{
    [SerializeField] Crystal Crystal;
    [SerializeField] Vector3 CrystalStartPosition;
    [SerializeField] SegmentSpawner SegmentSpawner;
    [SerializeField] int numberOfSegments = 10;
    [SerializeField] int straightLineLength = 400;

    public static new ShipTypes PlayerShipType = ShipTypes.Rhino;

    protected override void Start()
    {
        base.Start();
        SegmentSpawner.Seed = new System.Random().Next();
        numberOfSegments = numberOfSegments * IntensityLevel;
    }

    protected override void SetupTurn()
    {
        base.SetupTurn();

        SegmentSpawner.numberOfSegments = numberOfSegments;
        SegmentSpawner.StraightLineLength = straightLineLength / IntensityLevel;

        TrailSpawner.NukeTheTrails();
        Crystal.transform.position = CrystalStartPosition;

        SegmentSpawner.Initialize();
    }
}