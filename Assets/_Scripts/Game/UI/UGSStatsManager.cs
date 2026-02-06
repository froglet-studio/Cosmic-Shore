using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Services.Auth;
using CosmicShore.Soap;
using Unity.Services.Analytics;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace CosmicShore.Game.Analytics
{
    public class UGSStatsManager : MonoBehaviour
    {
        public static UGSStatsManager Instance { get; private set; }

        [Header("Config")] [SerializeField] GameDataSO gameData;

        [Header("Leaderboards")] [SerializeField]
        string blitzLeaderboardId = "wildlife_blitz_highscore";

        [SerializeField] string hexRaceLeaderboardId = "hex_race_time_trial";

        private PlayerStatsProfile _cachedProfile = new PlayerStatsProfile();
        private const string CLOUD_KEY = "PLAYER_STATS_PROFILE";
        private bool _isReady = false;

        void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
            // [Visual Note] Simple retry logic for auth
            try
            {
                while (AuthenticationController.Instance == null || !AuthenticationController.Instance.IsSignedIn)
                {
                    await Task.Delay(500);
                }

                _isReady = true;
                Debug.Log($"<color=green>[StatsManager] Auth Detected! Loading Profile...</color>");
                LoadProfile();
            }
            catch (Exception)
            {
                // Silent fail or retry later
            }
        }

        #region Public API - Wildlife Blitz

        public int GetHighScoreForCurrentMode()
        {
            if (!_isReady) return 0;
            string key = GetCurrentModeKey();
            // [Visual Note] Accessing sub-profile BlitzStats
            return _cachedProfile.BlitzStats.HighScores.TryGetValue(key, out int score) ? score : 0;
        }

        public void ReportMatchStats(int crystals, int lifeForms, int score)
        {
            if (!_isReady) return;

            // 1. Update Accumulative (Blitz specific)
            _cachedProfile.BlitzStats.LifetimeCrystalsCollected += crystals;
            _cachedProfile.BlitzStats.LifetimeLifeFormsKilled += lifeForms;

            // Global stats
            _cachedProfile.TotalGamesPlayed++;

            // 2. High Score Logic
            string key = GetCurrentModeKey();
            bool isNewRecord = _cachedProfile.BlitzStats.TryUpdateHighScore(key, score);

            Debug.Log($"[StatsManager] Blitz Stats. New Record: {isNewRecord} | Score: {score}");

            // 3. Leaderboard & Save
            if (isNewRecord)
            {
                SubmitScoreToLeaderboard(blitzLeaderboardId, score);
            }

            SaveProfile();
        }

        #endregion

        #region Public API - Hex Race

        public float GetBestRaceTime()
        {
            if (!_isReady) return 0f;
            string key = GetCurrentModeKey();

            // Return stored best time, or 0 if none exists
            if (_cachedProfile.HexRaceStats.BestRaceTimes.TryGetValue(key, out float time))
            {
                return time;
            }

            return 0f;
        }

        // [Visual Note] 2. Updated Report Logic
        public void ReportHexRaceStats(int cleanCrystals, float maxDrift, float maxBoost, float raceTime)
        {
            if (!_isReady)
            {
                Debug.LogWarning("[StatsManager] Not ready yet. Stats dropped.");
                return;
            }

            // 1. Update Accumulators
            _cachedProfile.HexRaceStats.TotalCleanCrystalsCollected += cleanCrystals;
            _cachedProfile.HexRaceStats.TotalDriftTime += maxDrift;
            _cachedProfile.TotalGamesPlayed++;

            // 2. Update Skill Records
            if (maxDrift > _cachedProfile.HexRaceStats.LongestSingleDrift)
                _cachedProfile.HexRaceStats.LongestSingleDrift = maxDrift;

            if (maxBoost > _cachedProfile.HexRaceStats.MaxTimeAtHighBoost)
                _cachedProfile.HexRaceStats.MaxTimeAtHighBoost = maxBoost;

            // 3. Race Time Logic (Lower is better)
            string key = GetCurrentModeKey();

            // [Visual Note] Only update local record if raw score is < 10000 (meaning it was a valid run)
            bool isNewRecord = false;
            if (raceTime < 10000f)
            {
                isNewRecord = _cachedProfile.HexRaceStats.TryUpdateBestTime(key, raceTime);
            }

            Debug.Log($"[StatsManager] Hex Race Stats. New Best Time: {isNewRecord} | Time: {raceTime}");

            // 4. Leaderboard Submission
            // ONLY submit if it was a valid run (Time < 10000)
            if (raceTime < 10000f)
            {
                // [Visual Note] We submit the raw seconds. 
                // IMPORTANT: Dashboard Leaderboard must be configured as ASCENDING.
                SubmitScoreToLeaderboard(hexRaceLeaderboardId, raceTime);
            }

            // 5. Force Cloud Save
            SaveProfile();
        }

        #endregion

        #region Public API - General

        public void TrackPlayAgain()
        {
            if (!_isReady) return;

            _cachedProfile.TotalPlayAgainPressed++;

            var replayEvent = new CustomEvent("replayButtonClicked")
            {
                { "screen_source", "end_game_scoreboard" },
                { "total_replays_session", _cachedProfile.TotalPlayAgainPressed }
            };

            AnalyticsService.Instance.RecordEvent(replayEvent);
            SaveProfile();
        }

        #endregion

        #region Helpers

        string GetCurrentModeKey()
        {
            string typeSuffix = gameData.IsMultiplayerMode ? "MP" : "SP";
            // Ensure GameMode string is valid (e.g. "WildlifeBlitz", "HexRace")
            return $"{gameData.GameMode}_{gameData.SelectedIntensity.Value}_{typeSuffix}";
        }

        // [Visual Note] Made generic to accept ID
        async void SubmitScoreToLeaderboard(string leaderboardId, double score)
        {
            try
            {
                await LeaderboardsService.Instance.AddPlayerScoreAsync(leaderboardId, score);
                Debug.Log($"<color=yellow>[StatsManager] Score {score} submitted to {leaderboardId}</color>");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[StatsManager] Leaderboard Error: {e.Message}");
            }
        }

        async void LoadProfile()
        {
            try
            {
                var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CLOUD_KEY });
                if (data.TryGetValue(CLOUD_KEY, out var item))
                {
                    _cachedProfile = item.Value.GetAs<PlayerStatsProfile>();

                    // [Visual Note] Ensure sub-objects exist if loading old data
                    if (_cachedProfile.BlitzStats == null)
                        _cachedProfile.BlitzStats = new WildlifeBlitzPlayerStatsProfile();
                    if (_cachedProfile.HexRaceStats == null)
                        _cachedProfile.HexRaceStats = new HexRacePlayerStatsProfile();

                    Debug.Log($"[StatsManager] Profile Loaded. Games Played: {_cachedProfile.TotalGamesPlayed}");
                }
            }
            catch
            {
                Debug.Log("[StatsManager] No profile found, creating new.");
            }
        }

        async void SaveProfile()
        {
            try
            {
                var data = new Dictionary<string, object> { { CLOUD_KEY, _cachedProfile } };
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                Debug.Log("[StatsManager] Profile Saved to Cloud.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StatsManager] Save Failed: {e.Message}");
            }
        }

        #endregion
    }
}