using UnityEngine;


namespace CosmicShore.Game.Arcade
{
    /// <summary>Concrete mini‑game that spawns a trail course of segments and a crystal pickup.
    /// </summary>
    public class FreestyleController : SinglePlayerMiniGameControllerBase
    {
        /*[Header("Course Settings")]
        [SerializeField] Crystal crystal;
        [SerializeField] Vector3 crystalStartPosition;*/
        
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
        
        // Vector3 crystalStart => scaleCrystalPositionWithIntensity ? crystalStartPosition * miniGameData.SelectedIntensity : crystalStartPosition;
        
        protected override void OnCountdownTimerEnded()
        {
            segmentSpawner.Seed = Random.Range(int.MinValue,int.MaxValue);
            
            // Scoring mode should never be dependent on Vessel Class.
            /*if (_miniGameData.Value.SelectedShipClass.Value == ShipClassType.Rhino) 
                scoreTracker.ScoringMode = ScoringModes.HostileVolumeDestroyed;*/

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
            if(resetEnvironmentOnEachTurn) 
                ResetEnvironment();
            
            base.SetupNewTurn();
        }

        void ResetEnvironment() {
            segmentSpawner.NumberOfSegments   = numberOfSegments;
            segmentSpawner.StraightLineLength = straightLineLength;
            // VesselPrismController.ClearTrails();
            // crystal.transform.position = crystalStart;
            segmentSpawner.Initialize();
        }
    }
}