using UnityEngine;
public class RampageMiniGame : MiniGame
{
    [SerializeField] Crystal Crystal;
    [SerializeField] Vector3 CrystalStartPosition;
    [SerializeField] SegmentSpawner SegmentSpawner;
    [SerializeField] SpawnableEllipsoid spawnableEllipsoid;
    int maxDifficulty = 4;
    float maxSize = 100;

    public static new ShipTypes PlayerShipType = ShipTypes.Rhino;

    protected override void Start()
    {
        base.Start();

        SegmentSpawner.Seed = new System.Random().Next();
    }

    protected override void SetupTurn()
    {
        base.SetupTurn();

        SegmentSpawner.numberOfSegments = 20;
        spawnableEllipsoid.maxlength = spawnableEllipsoid.maxwidth = spawnableEllipsoid.maxheight = maxSize * IntensityLevel / maxDifficulty;

        TrailSpawner.NukeTheTrails();
        Crystal.transform.position = CrystalStartPosition;

        SegmentSpawner.Initialize();
    }
}