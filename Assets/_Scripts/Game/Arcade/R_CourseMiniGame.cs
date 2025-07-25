using CosmicShore.Events;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game.Arcade
{
    /// <summary>Concrete mini‑game that spawns a trail course of segments and a crystal pickup.</summary>
    public class R_CourseMiniGame : R_MiniGameBase
    {
        [Header("Course Settings")]
        [SerializeField] Crystal crystal;
        [SerializeField] Vector3 crystalStartPosition;
        [SerializeField] SegmentSpawner segmentSpawner;
        [SerializeField] int baseNumberOfSegments = 10;
        [SerializeField] int baseStraightLineLength = 400;
        [FormerlySerializedAs("resetTrailsEachTurn")] [SerializeField] bool resetEnvironmentOnEachTurn = true;
        [SerializeField] bool scaleCrystalPositionWithIntensity = true;
        [SerializeField] bool scaleLengthWithIntensity = true;
        [SerializeField] bool scaleSegmentsWithIntensity = true;
        [Header("Helix Optional")]
        [SerializeField] SpawnableHelix helix;
        [SerializeField] float helixIntensityScaling = 1.3f;
        

        int numberOfSegments => scaleSegmentsWithIntensity ? baseNumberOfSegments * _miniGameData.Value.SelectedIntensity : baseNumberOfSegments;
        int straightLineLength => scaleLengthWithIntensity ? baseStraightLineLength / _miniGameData.Value.SelectedIntensity : baseStraightLineLength;
        Vector3 crystalStart => scaleCrystalPositionWithIntensity ? crystalStartPosition * _miniGameData.Value.SelectedIntensity : crystalStartPosition;

        protected override void OnMiniGameReady() 
        {
            segmentSpawner.Seed = Random.Range(int.MinValue,int.MaxValue);
            
            if (_miniGameData.Value.SelectedShipClass.Value == ShipClassType.Rhino) 
                scoreTracker.ScoringMode = ScoringModes.HostileVolumeDestroyed;

            if (helix)
            {
                helix.firstOrderRadius = helix.secondOrderRadius = _miniGameData.Value.SelectedIntensity.Value / helixIntensityScaling;
            }
            
            if (resetEnvironmentOnEachTurn) 
                ResetEnvironment();
            
            if (gameMode == GameModes.Freestyle) 
                FTUEEventManager.RaiseGameModeStarted(GameModes.Freestyle);
        }

        protected override void SetupNewTurn() {
            base.SetupNewTurn();
            if(resetEnvironmentOnEachTurn) ResetEnvironment();
        }

        void ResetEnvironment() {
            segmentSpawner.NumberOfSegments   = numberOfSegments;
            segmentSpawner.StraightLineLength = straightLineLength;
            TrailSpawner.NukeTheTrails();
            crystal.transform.position = crystalStart;
            segmentSpawner.Initialize();
        }
    }
}