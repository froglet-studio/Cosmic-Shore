using UnityEngine;
using System.Collections.Generic;

namespace CosmicShore.Game.Arcade
{
    public class MazeMiniGame : MiniGame
    {
        [SerializeField] Crystal Crystal;
        [SerializeField] Vector3 CrystalStartPosition;
        [SerializeField] SegmentSpawner SegmentSpawner;


        //public static virtual ShipTypes PlayerShipType = ShipTypes.Rhino;

        protected override void Start()
        {
            base.Start();
        }

        protected override void SetupTurn()
        {
            base.SetupTurn();
            InitializeSegments();
        }

        void InitializeSegments()
        {

            // VesselPrismController.ClearTrails();

            for (int i = 0; i < IntensityLevel; i++)
            {
                var maze = Instantiate(SegmentSpawner);
                maze.Seed = new System.Random().Next();
                maze.origin = Random.insideUnitSphere * 400;
                maze.RotationAmount = Random.Range(0, 360);
                maze.IntensityLevel = IntensityLevel;
            }
        }
    }
}