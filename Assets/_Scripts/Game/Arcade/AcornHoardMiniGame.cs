using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Acorn Hoard (Single Player): Fly through a dense arena of neutral prism clusters,
    /// stealing as much volume as possible. Uses the Squirrel's drift and steal mechanics.
    /// Turn ends when the volume stolen threshold is reached.
    /// Score = time to reach the threshold (lower is better).
    /// </summary>
    public class AcornHoardMiniGame : SinglePlayerMiniGameControllerBase
    {
        [Header("Arena")]
        [SerializeField] SegmentSpawner segmentSpawner;
        [SerializeField] int baseNumberOfSegments = 20;
        [SerializeField] bool scaleSegmentsWithIntensity = true;

        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);

        protected override bool UseGolfRules => true;

        protected override void Start()
        {
            InitializeArena();
            base.Start();
        }

        void InitializeArena()
        {
            if (!segmentSpawner) return;

            segmentSpawner.Seed = new System.Random().Next();
            segmentSpawner.NumberOfSegments = scaleSegmentsWithIntensity
                ? baseNumberOfSegments * Intensity
                : baseNumberOfSegments;

            segmentSpawner.Initialize();
        }

        protected override void OnResetForReplay()
        {
            InitializeArena();
            base.OnResetForReplay();
        }
    }
}
