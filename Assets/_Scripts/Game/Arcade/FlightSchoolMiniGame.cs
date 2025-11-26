using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class FlightSchoolMiniGame : MiniGame
    {
        [SerializeField] Crystal Crystal;
        [SerializeField] Vector3 CrystalStartPosition;
        [SerializeField] Vector3 CrystalStartScale = Vector3.one;
        [SerializeField] SegmentSpawner SegmentSpawner;

        protected override void Start()
        {
            base.Start();

            Crystal.transform.position = CrystalStartPosition;
            Crystal.transform.localScale = CrystalStartScale;
            Crystal.SetOrigin(CrystalStartPosition);

            SegmentSpawner.Seed = new System.Random().Next();
            SegmentSpawner.NumberOfSegments = IntensityLevel * 2 - 1;
            SegmentSpawner.origin.z = -(IntensityLevel - 1) * SegmentSpawner.StraightLineLength;
            SegmentSpawner.Initialize();
        }

        protected override void Update()
        {
            base.Update();

            if (!gameRunning) return;

            /*foreach (var turnMonitor in TurnMonitors)
            {
                if (turnMonitor.CheckForEndOfTurn())
                {
                    if (turnMonitor.ShouldEliminatePlayer())
                    {
                        EliminateActivePlayer();
                    }
                    EndTurn();
                    return;
                }
            }*/
        }

        protected override void SetupTurn()
        {
            base.SetupTurn();
            Crystal.transform.position = CrystalStartPosition;
            ActivePlayer.Ship.DisableSkimmer();
        }
    }
}