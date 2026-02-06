using CosmicShore.Game.IO;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerHexRaceController : MultiplayerDomainGamesController
    {
        [Header("Course")]
        [SerializeField] SegmentSpawner segmentSpawner;

        [SerializeField, Min(1)] int baseNumberOfSegments = 10;
        [SerializeField, Min(1)] int baseStraightLineLength = 400;

        [SerializeField] bool resetEnvironmentOnEachTurn = true;
        [SerializeField] bool scaleNumberOfSegmentsWithIntensity = true;
        [SerializeField] bool scaleLengthWithIntensity = true;

        [Header("Helix")]
        [SerializeField] SpawnableHelix helix;
        [SerializeField, Min(0.01f)] float helixIntensityScaling = 1.3f;

        [Header("Seed")]
        [SerializeField] int seed = 0;

        bool _environmentInitialized;
        
        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);

        // Override the base ClientRpc to ensure we don't start the turn before the environment exists
        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;

            // 1. Generate Seed on Server
            int currentSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
            // 2. Send Seed to ALL clients (including Host) to generate the world
            InitializeEnvironment_ClientRpc(currentSeed);
        }

        [ClientRpc]
        void InitializeEnvironment_ClientRpc(int syncedSeed)
        {
            int numberOfSegments = scaleNumberOfSegmentsWithIntensity ? baseNumberOfSegments * Intensity : baseNumberOfSegments;
            int straightLineLength = scaleLengthWithIntensity ? baseStraightLineLength / Intensity : baseStraightLineLength;

            if (segmentSpawner)
            {
                segmentSpawner.Seed = syncedSeed;
                segmentSpawner.NumberOfSegments = numberOfSegments;
                segmentSpawner.StraightLineLength = straightLineLength;
                
                ApplyHelixIntensity();
                segmentSpawner.Initialize();
            }
            else
            {
                Debug.LogError("SegmentSpawner reference missing on HexRaceController!");
            }
            gameData.SetPlayersActive();
            gameData.StartTurn();
        }

        void ApplyHelixIntensity()
        {
            if (!helix) return;
            var radius = Intensity / helixIntensityScaling;
            helix.firstOrderRadius = radius;
            helix.secondOrderRadius = radius;
        }
    }
}