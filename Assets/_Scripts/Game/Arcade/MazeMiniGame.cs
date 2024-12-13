using CosmicShore.Environment.FlowField;
using UnityEngine;
using System.Collections.Generic;

namespace CosmicShore.Game.Arcade
{
    public class MazeMiniGame : MiniGame
    {
        [SerializeField] Crystal Crystal;
        [SerializeField] Vector3 CrystalStartPosition;
        [SerializeField] List<SegmentSpawner> SegmentSpawners;
        [SerializeField] SpawnableWall wall;



        //public static virtual ShipTypes PlayerShipType = ShipTypes.Rhino;

        protected override void Start()
        {
            base.Start();
            InitializeSegments();
        }

        protected override void SetupTurn()
        {
            base.SetupTurn();
            InitializeSegments();
        }

        void InitializeSegments()
        {

            TrailSpawner.NukeTheTrails();

            foreach (var segmentSpawner in SegmentSpawners)
            {
                segmentSpawner.Initialize();
                segmentSpawner.Seed = new System.Random().Next();
                wall.Height = 6 - IntensityLevel;
                wall.Width = 6 - IntensityLevel;
            }

        }
    }
}