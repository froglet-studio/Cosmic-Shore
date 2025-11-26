using CosmicShore.Events;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    // TODO - DEPRECATED SCRIPT, Use R_CourseMiniGame instead
    public class CourseMiniGame : MiniGame
    {
        [SerializeField] Crystal Crystal;
        [SerializeField] Vector3 CrystalStartPosition;
        [SerializeField] SegmentSpawner SegmentSpawner;
        [SerializeField] int numberOfSegments = 10;
        [SerializeField] int straightLineLength = 400;
        [SerializeField] bool ResetTrails = true;
        [SerializeField] bool ScaleCrystalPositionWithIntensity;
        [SerializeField] bool ScaleLengthWithIntensity = true;
        [SerializeField] bool ScaleNumberOfSegmentsWithIntensity = true;
        [SerializeField] SpawnableHelix helix;
        [SerializeField] float helixIntensitycaling = 1.3f;

        //public

        protected override void Start()
        {
            base.Start();
            SegmentSpawner.Seed = new System.Random().Next();
            if (ScaleNumberOfSegmentsWithIntensity) numberOfSegments *= IntensityLevel;

            // TODO - Scoring mode should not be dependent on Ship Class Type
            /*if (PlayerShipType == ShipClassType.Rhino)
                ScoreTracker.ScoringMode = ScoringModes.HostileVolumeDestroyed;*/

            if (helix) helix.firstOrderRadius = helix.secondOrderRadius = IntensityLevel / helixIntensitycaling;

            if (!ResetTrails)
            {
                InitializeTrails();
            }

            if (gameMode == GameModes.Freestyle)
            {
                FTUEEventManager.RaiseGameModeStarted(GameModes.Freestyle);
            }
        }

        protected override void SetupTurn()
        {
            base.SetupTurn();

            if (ResetTrails)
            {
                InitializeTrails();
            }
        }

        void InitializeTrails()
        {
            if (ScaleNumberOfSegmentsWithIntensity) SegmentSpawner.NumberOfSegments = numberOfSegments;
            if (ScaleLengthWithIntensity) SegmentSpawner.StraightLineLength = straightLineLength / IntensityLevel;

            TrailSpawner.NukeTheTrails();
            if (ScaleCrystalPositionWithIntensity) Crystal.transform.position = IntensityLevel * CrystalStartPosition;
            else Crystal.transform.position = CrystalStartPosition;

            SegmentSpawner.Initialize();
        }
    }
}