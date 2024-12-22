using CosmicShore.Environment.FlowField;
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

            for (int i = 0; i < 1; i++)
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