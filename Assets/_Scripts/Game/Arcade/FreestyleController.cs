using UnityEngine;


namespace CosmicShore.Game.Arcade
{
    /// <summary>Concrete mini‑game that spawns a trail course of segments and a crystal pickup.
    /// </summary>
    public class FreestyleController : SinglePlayerMiniGameControllerBase
    {
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
        

        int numberOfSegments => scaleSegmentsWithIntensity ? baseNumberOfSegments * gameData.SelectedIntensity : baseNumberOfSegments;
        int straightLineLength => scaleLengthWithIntensity ? baseStraightLineLength / gameData.SelectedIntensity : baseStraightLineLength;
        
        protected override void OnCountdownTimerEnded()
        {
            segmentSpawner.Seed = Random.Range(int.MinValue,int.MaxValue);
            if (helix)
            {
                helix.firstOrderRadius = helix.secondOrderRadius = gameData.SelectedIntensity.Value / helixIntensityScaling;
            }
            
            if (resetEnvironmentOnEachTurn) 
                ResetEnvironment();
            
            base.OnCountdownTimerEnded();
        }

        protected override void SetupNewTurn() 
        {
            RaiseToggleReadyButtonEvent(true);
            
            if(resetEnvironmentOnEachTurn) 
                ResetEnvironment();
            
            base.SetupNewTurn();
        }

        void ResetEnvironment() {
            segmentSpawner.NumberOfSegments   = numberOfSegments;
            segmentSpawner.StraightLineLength = straightLineLength;
            segmentSpawner.Initialize();
        }
    }
}