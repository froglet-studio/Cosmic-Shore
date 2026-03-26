using CosmicShore.Game;
using CosmicShore.Soap;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Game.Fauna
{
    /// <summary>
    /// Monitors the total volume of prisms within a Cell and exposes a normalized
    /// Aggression level (0-1) that fauna spawning systems use to control birth rates
    /// and consumption intensity.
    ///
    /// Aggression = 0 means prism count is below the activation threshold (no locusts needed).
    /// Aggression = 1 means prism count has reached the critical ceiling (maximum culling urgency).
    ///
    /// This component should be placed on or near the Cell GameObject.
    /// </summary>
    public class CellAggressionManager : MonoBehaviour
    {
        [Header("Cell Reference")]
        [SerializeField] CellRuntimeDataSO cellRuntime;
        [SerializeField] GameDataSO gameData;

        [Header("Thresholds")]
        [Tooltip("Total prism volume below which aggression is 0 (no locusts spawn).")]
        [SerializeField] float activationVolumeThreshold = 500f;
        [Tooltip("Total prism volume at which aggression reaches 1.0 (maximum urgency).")]
        [SerializeField] float criticalVolumeCeiling = 3000f;

        [Header("Update Settings")]
        [Tooltip("How often (seconds) to recalculate aggression level.")]
        [SerializeField] float updateInterval = 2f;

        [Header("Smoothing")]
        [Tooltip("How quickly aggression ramps up/down. Higher = more responsive.")]
        [SerializeField] float aggressionSmoothSpeed = 2f;

        float rawAggression;
        float smoothedAggression;

        /// <summary>
        /// The current smoothed aggression level, 0 to 1.
        /// </summary>
        public float Aggression => smoothedAggression;

        /// <summary>
        /// Whether the system has crossed the activation threshold at least once.
        /// Once active, locusts may continue until prism volume drops back below threshold.
        /// </summary>
        public bool IsActive => smoothedAggression > 0f;

        float nextUpdateTime;

        void Update()
        {
            if (Time.time < nextUpdateTime) return;
            nextUpdateTime = Time.time + updateInterval;

            float totalVolume = GetTotalPrismVolume();
            rawAggression = CalculateAggression(totalVolume);
            smoothedAggression = Mathf.MoveTowards(smoothedAggression, rawAggression, aggressionSmoothSpeed * updateInterval);
        }

        float CalculateAggression(float totalVolume)
        {
            if (totalVolume <= activationVolumeThreshold)
                return 0f;

            float range = criticalVolumeCeiling - activationVolumeThreshold;
            if (range <= 0f) return 1f;

            return Mathf.Clamp01((totalVolume - activationVolumeThreshold) / range);
        }

        float GetTotalPrismVolume()
        {
            // Primary: use GameDataSO total volume which aggregates all team volumes
            if (gameData)
                return gameData.GetTotalVolume();

            // Fallback: if a Cell reference is available, sum team volumes directly
            if (cellRuntime && cellRuntime.Cell)
            {
                var cell = cellRuntime.Cell;
                float total = 0f;
                total += cell.GetTeamVolume(Domains.Jade);
                total += cell.GetTeamVolume(Domains.Ruby);
                total += cell.GetTeamVolume(Domains.Gold);
                total += cell.GetTeamVolume(Domains.Blue);
                return total;
            }

            return 0f;
        }
    }
}
