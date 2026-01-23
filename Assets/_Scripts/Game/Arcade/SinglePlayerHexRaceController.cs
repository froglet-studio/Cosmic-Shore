using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Hex Race
    /// </summary>>
    public class SinglePlayerHexRaceController : SinglePlayerMiniGameControllerBase
    {
        [Header("Course")]
        [SerializeField] SegmentSpawner segmentSpawner;

        [SerializeField, Min(1)] int baseNumberOfSegments = 10;
        [SerializeField, Min(1)] int baseStraightLineLength = 400;

        [SerializeField] bool resetEnvironmentOnEachTurn = true;

        [SerializeField] bool scaleNumberOfSegmentsWithIntensity = true;
        [SerializeField] bool scaleLengthWithIntensity = true;

        [Header("Helix (Optional)")]
        [SerializeField] SpawnableHelix helix;
        [SerializeField, Min(0.01f)] float helixIntensityScaling = 1.3f;

        [Header("Seed")]
        [Tooltip("If 0, a random seed is used each turn. If non-zero, this fixed seed is used.")]
        [SerializeField] int seed = 0;

        bool _environmentInitialized;

        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);

        int NumberOfSegments =>
            scaleNumberOfSegmentsWithIntensity ? baseNumberOfSegments * Intensity : baseNumberOfSegments;

        int StraightLineLength =>
            scaleLengthWithIntensity ? baseStraightLineLength / Intensity : baseStraightLineLength;

        protected override void SetupNewTurn()
        {
            RaiseToggleReadyButtonEvent(true);

            // Old behavior:
            // - ResetTrails == true  -> InitializeTrails() each SetupTurn
            // - ResetTrails == false -> InitializeTrails() once at Start
            if (resetEnvironmentOnEachTurn)
            {
                ResetEnvironment();
            }
            else if (!_environmentInitialized)
            {
                ResetEnvironment();
                _environmentInitialized = true;
            }

            base.SetupNewTurn();
        }

        protected override void OnCountdownTimerEnded()
        {
            segmentSpawner.Seed = (seed != 0)
                ? seed
                : Random.Range(int.MinValue, int.MaxValue);

            ApplyHelixIntensity();

            if (resetEnvironmentOnEachTurn)
                ResetEnvironment();

            base.OnCountdownTimerEnded();
        }

        void ResetEnvironment()
        {
            if (!segmentSpawner)
            {
                Debug.LogError($"{nameof(SinglePlayerSlipnStrideController)} missing {nameof(segmentSpawner)}.", this);
                return;
            }

            segmentSpawner.NumberOfSegments = NumberOfSegments;
            segmentSpawner.StraightLineLength = StraightLineLength;

            ApplyHelixIntensity();
            segmentSpawner.Initialize();
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