using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class RampageMiniGame : MiniGame
    {
        [SerializeField] Crystal Crystal;
        [SerializeField] Vector3 CrystalStartPosition;
        [SerializeField] SegmentSpawner SegmentSpawner;
        [SerializeField] SpawnableEllipsoid spawnableEllipsoid;
        int maxDifficulty = 4;
        [SerializeField] float maxSize = 100;
        [SerializeField] float maxSphereRadius = 100;
        [SerializeField] int initialSegments = 100;
        [SerializeField] int intensitySegments = 50;

        public static new VesselClassType PlayerVesselType = VesselClassType.Rhino;

        protected override void Start()
        {
            base.Start();
            SegmentSpawner.Seed = new System.Random().Next();
        }

        protected override void SetupTurn()
        {
            base.SetupTurn();

            SegmentSpawner.Radius = maxSphereRadius * IntensityLevel;
            SegmentSpawner.NumberOfSegments = initialSegments + (intensitySegments * (IntensityLevel-1));
            spawnableEllipsoid.maxlength = spawnableEllipsoid.maxwidth = spawnableEllipsoid.maxheight = maxSize * IntensityLevel / maxDifficulty;

            VesselPrismController.NukeTheTrails();
            Crystal.transform.position = CrystalStartPosition;

            SegmentSpawner.Initialize();
        }
    }
}