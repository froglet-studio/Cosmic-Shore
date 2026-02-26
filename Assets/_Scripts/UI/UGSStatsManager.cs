using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Core;
using Unity.Services.Analytics;
using CosmicShore.ScriptableObjects;
using Unity.Services.CloudSave;
using Unity.Services.Leaderboards;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.UI
{
    public class UGSStatsManager : MonoBehaviour
    {
        [Header("Dependencies")] 
        [SerializeField] LeaderboardConfigSO leaderboardConfig; 
        
        [SerializeField]
        AuthenticationDataVariable authenticationDataVariable;
        AuthenticationData authenticationData => authenticationDataVariable.Value;

        private PlayerStatsProfile _cachedProfile = new PlayerStatsProfile();
        private VesselStatsCloudData _vesselStats = new VesselStatsCloudData();
        private const string CLOUD_KEY = UGSKeys.PlayerStatsProfile;
        private const string VESSEL_KEY = UGSKeys.VesselStats;
        private bool _isReady = false;

        // Save debouncing: coalesces rapid saves into a single cloud call
        private const float SAVE_DEBOUNCE_SECONDS = 2f;
        private bool _saveDirty;
        private bool _saveInFlight;

        private void OnEnable()
        {
            if (authenticationDataVariable == null)
            {
                Debug.LogWarning("[UGSStatsManager] authenticationDataVariable is not assigned — auth events will not be observed.");
                return;
            }
            authenticationData.OnSignedIn.OnRaised += OnAuthenticationSignedIn;
        }

        private void OnDisable()
        {
            if (authenticationDataVariable == null) return;
            authenticationData.OnSignedIn.OnRaised -= OnAuthenticationSignedIn;
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
                    cloudBest = _cachedProfile.MultiHexStats.BestMultiplayerRaceTimes.GetValueOrDefault(key, 0f);
                else if (mode == GameModes.MultiplayerJoust)
                    cloudBest = _cachedProfile.JoustStats.BestRaceTimes.GetValueOrDefault(key, 0f);
                
                if (cloudBest <= 0.001f) return currentSessionScore;
                return currentSessionScore >= 10000f ? cloudBest : Mathf.Min(cloudBest, currentSessionScore);
            }
            else if (mode == GameModes.WildlifeBlitz)
            {
                int cloudBest = _cachedProfile.BlitzStats.HighScores.GetValueOrDefault(key, 0);
                return Mathf.Max(cloudBest, currentSessionScore);
            }
            else if (mode == GameModes.MultiplayerCrystalCapture)
            {
                int cloudBest = _cachedProfile.CrystalCaptureStats.HighScores.GetValueOrDefault(key, 0);
                return Mathf.Max(cloudBest, currentSessionScore);
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
                    if (squirrel.MaxCleanStreak > stats.Counters.GetValueOrDefault("BestCleanStreak", 0))
                        stats.Counters["BestCleanStreak"] = squirrel.MaxCleanStreak;
                    break;
                default:
                    Debug.LogWarning($"[UGSStats] No vessel-specific handler for {telemetry.GetType().Name}");
                    break;
            }

            SaveVesselStats();
        }

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

        #endregion

        #region Internal

        void OnAuthenticationSignedIn()
        {
            _isReady = true;
            LoadProfile();
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

        async void LoadProfile()
        {
            try
            {
                var keys = new HashSet<string> { CLOUD_KEY, VESSEL_KEY };
                var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                if (data.TryGetValue(CLOUD_KEY, out var statsItem))
                    _cachedProfile = statsItem.Value.GetAs<PlayerStatsProfile>();

                if (data.TryGetValue(VESSEL_KEY, out var vesselItem))
                    _vesselStats = vesselItem.Value.GetAs<VesselStatsCloudData>();

                _cachedProfile.BlitzStats ??= new WildlifeBlitzPlayerStatsProfile();
                _cachedProfile.MultiHexStats ??= new HexRacePlayerStatsProfile();
                _cachedProfile.JoustStats ??= new JoustPlayerStatsProfile();
                _cachedProfile.CrystalCaptureStats ??= new CrystalCapturePlayerStatsProfile();
                _vesselStats ??= new VesselStatsCloudData();

                Debug.Log("[UGSStats] Profile and vessel stats loaded from cloud save.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UGSStats] Failed to load from cloud save: {ex.Message}. Using defaults.");
            }
        }

        /// <summary>
        /// Marks profile as dirty and schedules a debounced cloud save.
        /// Multiple calls within SAVE_DEBOUNCE_SECONDS collapse into one actual save.
        /// </summary>
        void SaveProfile()
        {
            _saveDirty = true;
            if (!_saveInFlight)
                DebouncedSaveAsync();
        }

        async void DebouncedSaveAsync()
        {
            if (_saveInFlight) return;
            _saveInFlight = true;

            try
            {
                // Wait to coalesce rapid mutations
                await Task.Delay((int)(SAVE_DEBOUNCE_SECONDS * 1000));

                // Drain: keep saving while mutations arrive during the save
                while (_saveDirty)
                {
                    _saveDirty = false;
                    _cachedProfile.LastLoginTick = DateTime.UtcNow.Ticks;
                    var data = new Dictionary<string, object> { { CLOUD_KEY, _cachedProfile } };
                    await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UGSStats] Save failed: {e.Message}");
            }
            finally
            {
                _saveInFlight = false;

                // If something dirtied during our save, kick off another cycle
                if (_saveDirty)
                    DebouncedSaveAsync();
            }
        }

        async void SaveVesselStats()
        {
            try
            {
                var data = new Dictionary<string, object> { { VESSEL_KEY, _vesselStats } };
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                Debug.Log("[UGSStats] Vessel stats saved to cloud.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[UGSStats] Vessel stats save failed: {e.Message}");
            }
        }

        #endregion
    }
}