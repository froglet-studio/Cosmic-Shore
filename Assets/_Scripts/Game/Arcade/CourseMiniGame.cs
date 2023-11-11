using CosmicShore.Environment.FlowField;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class CourseMiniGame : MiniGame
    {
        [SerializeField] Crystal Crystal;
        [SerializeField] Vector3 CrystalStartPosition;
        [SerializeField] SegmentSpawner SegmentSpawner;
        [SerializeField] int numberOfSegments = 10;
        [SerializeField] int straightLineLength = 400;
        [SerializeField] bool ResetTrails = true;

        //public static virtual ShipTypes PlayerShipType = ShipTypes.Rhino;

        protected override void Start()
        {
            base.Start();
            SegmentSpawner.Seed = new System.Random().Next();
            numberOfSegments = numberOfSegments * IntensityLevel;

            if (PlayerShipType == ShipTypes.Rhino)
                ScoreTracker.ScoringMode = ScoringModes.HostileVolumeDestroyed;

            if (!ResetTrails)
            {
                SegmentSpawner.numberOfSegments = numberOfSegments;
                SegmentSpawner.StraightLineLength = straightLineLength / IntensityLevel;

                TrailSpawner.NukeTheTrails();
                Crystal.transform.position = CrystalStartPosition;

                SegmentSpawner.Initialize();
            }
        }

        protected override void SetupTurn()
        {
            base.SetupTurn();

            if (ResetTrails)
            {
                SegmentSpawner.numberOfSegments = numberOfSegments;
                SegmentSpawner.StraightLineLength = straightLineLength / IntensityLevel;

                TrailSpawner.NukeTheTrails();
                Crystal.transform.position = CrystalStartPosition;

                SegmentSpawner.Initialize();
            }
        }
    }
}