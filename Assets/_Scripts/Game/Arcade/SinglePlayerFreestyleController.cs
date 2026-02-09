
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Freestyle game mode.
    /// Features: Endless gameplay, no end condition, procedural environment
    /// Flow: Start → Play infinitely (no end game)
    /// </summary>
    public class SinglePlayerFreestyleController : SinglePlayerMiniGameControllerBase
    {
        [Header("Environment")]
        [SerializeField] SegmentSpawner segmentSpawner;
        [SerializeField] int baseNumberOfSegments = 10;
        [SerializeField] int baseStraightLineLength = 400;
        [SerializeField] bool resetEnvironmentOnEachTurn = true;
        [SerializeField] bool scaleCrystalPositionWithIntensity = true;
        [SerializeField] bool scaleLengthWithIntensity = true;
        [SerializeField] bool scaleSegmentsWithIntensity = true;
        
        [Header("Helix Optional")]
        [SerializeField] SpawnableHelix helix;
        [SerializeField] float helixIntensityScaling = 1.3f;
        
        [Header("Seed")]
        [SerializeField] int Seed = 0;
        
        /// <summary>Freestyle has no end game - play forever!</summary>
        protected override bool HasEndGame => false;
        protected override bool ShowEndGameSequence => false;
        
        int numberOfSegments => scaleSegmentsWithIntensity 
            ? baseNumberOfSegments * gameData.SelectedIntensity.Value 
            : baseNumberOfSegments;
            
        int straightLineLength => scaleLengthWithIntensity 
            ? baseStraightLineLength / gameData.SelectedIntensity.Value 
            : baseStraightLineLength;
        
        protected override void OnCountdownTimerEnded()
        {
            // Set random or fixed seed
            segmentSpawner.Seed = Seed != 0 ? Seed : Random.Range(int.MinValue, int.MaxValue);
            
            // Apply helix intensity if helix exists
            if (helix)
            {
                helix.firstOrderRadius = helix.secondOrderRadius = 
                    gameData.SelectedIntensity.Value / helixIntensityScaling;
            }
            
            if (resetEnvironmentOnEachTurn) 
                ResetEnvironment();
            
            base.OnCountdownTimerEnded();
        }

        protected override void SetupNewTurn() 
        {
            RaiseToggleReadyButtonEvent(true);
            
            if (resetEnvironmentOnEachTurn) 
                ResetEnvironment();
            
            base.SetupNewTurn();
        }
        
        void ResetEnvironment() 
        {
            if (!segmentSpawner)
            {
                Debug.LogError($"Missing {nameof(segmentSpawner)} reference!", this);
                return;
            }
            
            segmentSpawner.NumberOfSegments = numberOfSegments;
            segmentSpawner.StraightLineLength = straightLineLength;
            segmentSpawner.Initialize();
        }
    }
}