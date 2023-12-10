using CosmicShore.Environment.FlowField;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class DriftCourseMinigame : MiniGame
    {
        [SerializeField] Crystal Crystal;
        [SerializeField] Vector3 CrystalStartPosition;
        [SerializeField] SegmentSpawner SegmentSpawner;
        [SerializeField] SpawnableEllipsoid spawnableEllipsoid;
        int maxDifficulty = 4;
        float maxSize = 100;
        float maxSphereRadius = 250;

        public static new ShipTypes PlayerShipType = ShipTypes.Rhino;

        protected override void Start()
        {
            base.Start();

            SegmentSpawner.Seed = new System.Random().Next();
        }

        protected override void SetupTurn()
        {
            base.SetupTurn();

            SegmentSpawner.Radius = maxSphereRadius * IntensityLevel;
            spawnableEllipsoid.maxlength = spawnableEllipsoid.maxwidth = spawnableEllipsoid.maxheight = maxSize * IntensityLevel / maxDifficulty;


            TrailSpawner.NukeTheTrails();
            Crystal.transform.position = CrystalStartPosition;


            SegmentSpawner.Initialize();
        }
    }
}
