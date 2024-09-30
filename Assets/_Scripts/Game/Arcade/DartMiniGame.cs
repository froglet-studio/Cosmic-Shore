using CosmicShore.Environment.FlowField;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class DartMiniGame : MiniGame
    {
        [SerializeField] SpawnableDartBoard DartBoard;
        [SerializeField] Crystal Crystal;
        [SerializeField] Vector3 CrystalStartPosition;
        [SerializeField] SegmentSpawner SegmentSpawner;

        [SerializeField] int resourceIndex = 0;

        protected override void Start()
        {
            base.Start();

            SegmentSpawner.Seed = new System.Random().Next();
            SegmentSpawner.DifficultyAngle = IntensityLevel * (140 / 4); // (180 - random buffer - dartbard arclength) / (max difficulty level)
            SegmentSpawner.Initialize();
        }

        protected override void SetupTurn()
        {
            base.SetupTurn();

            Silhouette silhouette;
            ActivePlayer.Ship.TryGetComponent<Silhouette>(out silhouette);
            silhouette.Clear();

            TrailSpawner.NukeTheTrails();
            Crystal.transform.position = CrystalStartPosition;
            ActivePlayer.Ship.ResourceSystem.ChangeResourceAmount(resourceIndex, ActivePlayer.Ship.ResourceSystem.Resources[resourceIndex].MaxAmount);
        }
    }
}