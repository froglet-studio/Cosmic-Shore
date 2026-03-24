using System;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using Unity.Services.Analytics;
using Unity.Services.Leaderboards;
using UnityEngine;
using CosmicShore.Utility;
using Reflex.Attributes;

namespace CosmicShore.Core
{
    /// <summary>
    /// Domain service for game stats and vessel telemetry.
    /// Delegates all cloud persistence to UGSDataService.StatsRepo and VesselStatsRepo.
    /// Keeps leaderboard submission, analytics, and stat evaluation logic here.
    /// </summary>
    public class UGSStatsManager : MonoBehaviour
    {
        public static UGSStatsManager Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] LeaderboardConfigSO leaderboardConfig;

        [Inject] UGSDataService _ugsDataService;

        private PlayerStatsProfile _cachedProfile = new();
        private VesselStatsCloudData _vesselStats = new();
        private bool _isReady;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (_ugsDataService.IsInitialized)
                HandleDataServiceReady();
            else
                _ugsDataService.OnInitialized += HandleDataServiceReady;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_ugsDataService != null)
                _ugsDataService.OnInitialized -= HandleDataServiceReady;
        }

        void HandleDataServiceReady()
        {
            _ugsDataService.OnInitialized -= HandleDataServiceReady;

            // Use the repo's data directly — no separate cloud load needed
            if (_ugsDataService.StatsRepo != null)
                _cachedProfile = _ugsDataService.StatsRepo.Data;
            if (_ugsDataService.VesselStatsRepo != null)
                _vesselStats = _ugsDataService.VesselStatsRepo.Data;

            _isReady = true;
            CSDebug.Log("[UGSStats] Initialized from UGSDataService repositories.");
        }

        #region Public API - Smart High Score Evaluation

        public float GetEvaluatedHighScore(GameModes mode, int intensity, float currentSessionScore)
        {
            if (!_isReady) return currentSessionScore;

            string key = $"{mode}_{intensity}";

            if (mode is GameModes.HexRace or GameModes.MultiplayerJoust)
            {
                float cloudBest = 0f;

                if (mode == GameModes.HexRace)
                    _cachedProfile.MultiHexStats.BestMultiplayerRaceTimes.TryGetValue(key, out cloudBest);
                else if (mode == GameModes.MultiplayerJoust)
                    _cachedProfile.JoustStats.BestRaceTimes.TryGetValue(key, out cloudBest);
                
                if (cloudBest <= 0.001f) return currentSessionScore;
                return currentSessionScore >= 10000f ? cloudBest : Mathf.Min(cloudBest, currentSessionScore);
            }
            else if (mode == GameModes.WildlifeBlitz)
            {
                _cachedProfile.BlitzStats.HighScores.TryGetValue(key, out int blitzBest);
                return Mathf.Max(blitzBest, currentSessionScore);
            }
            else if (mode == GameModes.MultiplayerCrystalCapture)
            {
                _cachedProfile.CrystalCaptureStats.HighScores.TryGetValue(key, out int ccBest);
                return Mathf.Max(ccBest, currentSessionScore);
            }

            return currentSessionScore;
        }

        #endregion

        #region Public API - Reporting

        public void ReportBlitzStats(GameModes mode, int intensity, int crystals, int lifeForms, int score)
        {
            if (!_isReady) return;

            string key = $"{mode}_{intensity}";
            _cachedProfile.BlitzStats.TryUpdateHighScore(key, score);

            SubmitScoreInternal(mode, intensity, score);
            SaveProfile();
        }

        public void ReportHexRaceStats(GameModes mode, int intensity, int clean, float drift, int jousts, float score)
        {
            if (!_isReady) return;

            string key = $"{mode}_{intensity}";
            if (score < 10000f)
            {
                _cachedProfile.MultiHexStats.TryUpdateBestTime(key, score);
                SubmitScoreInternal(mode, intensity, score);
            }

            SaveProfile();
        }

        public void ReportJoustStats(GameModes mode, int intensity, int joustsWon, float raceTime)
        {
            if (!_isReady) return;

            string key = $"{mode}_{intensity}";
            if (raceTime < 10000f)
            {
                _cachedProfile.JoustStats.TryUpdateBestTime(key, raceTime);
                SubmitScoreInternal(mode, intensity, raceTime);
            }

            SaveProfile();
        }

        public void ReportCrystalCaptureStats(GameModes mode, int intensity, int crystals)
        {
            if (!_isReady) return;

            string key = $"{mode}_{intensity}";
            _cachedProfile.CrystalCaptureStats.TryUpdateHighScore(key, crystals);

            SubmitScoreInternal(mode, intensity, crystals);
            SaveProfile();
        }

        /// <summary>
        /// Reports per-vessel telemetry stats to UGS Cloud Save at game end.
        /// Called by score trackers after they read the vessel's telemetry.
        /// </summary>
        public void ReportVesselTelemetry(VesselTelemetry telemetry, string vesselTypeName)
        {
            if (!_isReady || telemetry == null || string.IsNullOrEmpty(vesselTypeName))
            {
                Debug.LogWarning($"[UGSStats] ReportVesselTelemetry skipped — " +
                    $"ready={_isReady}, telemetry={(telemetry != null ? telemetry.GetType().Name : "NULL")}, " +
                    $"vessel='{vesselTypeName}'");
                return;
            }

            Debug.Log($"[UGSStats] ReportVesselTelemetry — {telemetry.GetType().Name} for '{vesselTypeName}', " +
                $"drift={telemetry.MaxDriftTime:F2}s, boost={telemetry.MaxBoostTime:F2}s, " +
                $"prismsDmg={telemetry.PrismsDamaged}");

            var stats = _vesselStats.GetOrCreate(vesselTypeName);
            stats.GamesPlayed++;

            // Common stats — keep best values
            if (telemetry.MaxDriftTime > stats.BestDriftTime)
                stats.BestDriftTime = telemetry.MaxDriftTime;
            if (telemetry.MaxBoostTime > stats.BestBoostTime)
                stats.BestBoostTime = telemetry.MaxBoostTime;
            stats.TotalPrismsDamaged += telemetry.PrismsDamaged;

            // Vessel-specific stats
            switch (telemetry)
            {
                case SparrowVesselTelemetry sparrow:
                    Debug.Log($"[UGSStats] Sparrow stats — prismBlocks={sparrow.PrismBlocksShot}, " +
                        $"skyburst={sparrow.SkyburstMissilesShot}, dangerBlocks={sparrow.DangerBlocksSpawned}");
                    stats.IncrementCounter("PrismBlocksShot", sparrow.PrismBlocksShot);
                    stats.IncrementCounter("SkyburstMissilesShot", sparrow.SkyburstMissilesShot);
                    stats.IncrementCounter("DangerBlocksSpawned", sparrow.DangerBlocksSpawned);
                    break;
                case SquirrelVesselTelemetry squirrel:
                    Debug.Log($"[UGSStats] Squirrel stats — jousts={squirrel.JoustsWon}, " +
                        $"stolen={squirrel.PrismsStolen}, cleanStreak={squirrel.MaxCleanStreak}");
                    stats.IncrementCounter("JoustsWon", squirrel.JoustsWon);
                    stats.IncrementCounter("PrismsStolen", squirrel.PrismsStolen);
                    stats.Counters.TryGetValue("BestCleanStreak", out int prevStreak);
                    if (squirrel.MaxCleanStreak > prevStreak)
                        stats.Counters["BestCleanStreak"] = squirrel.MaxCleanStreak;
                    break;
                default:
                    Debug.LogWarning($"[UGSStats] No vessel-specific handler for {telemetry.GetType().Name}");
                    break;
            }

            SaveVesselStats();
        }

        #endregion

        #region Internal

        public void TrackPlayAgain()
        {
            try
            {
                var evt = new CustomEvent(UGSKeys.EventPlayAgain);
                AnalyticsService.Instance.RecordEvent(evt);
                AnalyticsService.Instance.Flush();
                Debug.Log("[UGSStats] Play Again analytics event sent.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UGSStats] Failed to send Play Again event: {ex.Message}");
            }
        }

        async void SubmitScoreInternal(GameModes mode, int intensity, double score)
        {
            try
            {
                string id = leaderboardConfig.GetLeaderboardId(mode, intensity);
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"[UGSStats] No leaderboard mapping for {mode} intensity {intensity}");
                    return;
                }

                await LeaderboardsService.Instance.AddPlayerScoreAsync(id, score);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UGSStats] Leaderboard submit failed for {mode}/{intensity}: {ex.Message}");
            }
        }

        /// <summary>
        /// Delegates save to StatsRepo (debounced by the repository).
        /// </summary>
        void SaveProfile()
        {
            _cachedProfile.LastLoginTick = DateTime.UtcNow.Ticks;
            _ugsDataService?.StatsRepo?.MarkDirty();
        }

        void SaveVesselStats()
        {
            _ugsDataService?.VesselStatsRepo?.MarkDirty();
        }

        #endregion
    }
}
