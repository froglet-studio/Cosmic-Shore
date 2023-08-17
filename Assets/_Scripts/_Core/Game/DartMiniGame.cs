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

        SegmentSpawner.Seed = new System.Random().Next();
        SegmentSpawner.DifficultyAngle = DifficultyLevel * (140 / 4); // (180 - random buffer - dartbard arclength) / (max difficulty level)
        SegmentSpawner.Initialize();
    }

    protected override void SetupTurn()
    {
        base.SetupTurn();

        TrailSpawner.NukeTheTrails();
        Crystal.transform.position = CrystalStartPosition;
    }
}