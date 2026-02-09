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

        [Header("Dependencies")] 
        [SerializeField] GameDataSO gameData;
        [SerializeField] LeaderboardConfigSO leaderboardConfig; 

        private PlayerStatsProfile _cachedProfile = new PlayerStatsProfile();
        private const string CLOUD_KEY = "PLAYER_STATS_PROFILE";
        private bool _isReady = false;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
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
                //TODO
            }
        }

        #region Public API - Wildlife Blitz

        public int GetHighScoreForCurrentMode()
        {
            if (!_isReady) return 0;
            string key = GetCurrentModeAndIntensityKey();
            return _cachedProfile.BlitzStats.HighScores.GetValueOrDefault(key, 0);
        }

        public void ReportMatchStats(int crystals, int lifeForms, int score)
        {
            if (!_isReady) return;

            _cachedProfile.BlitzStats.LifetimeCrystalsCollected += crystals;
            _cachedProfile.BlitzStats.LifetimeLifeFormsKilled += lifeForms;
            _cachedProfile.TotalGamesPlayed++;

            string key = GetCurrentModeAndIntensityKey();
            bool isNewRecord = _cachedProfile.BlitzStats.TryUpdateHighScore(key, score);

            Debug.Log($"[StatsManager] Blitz Stats. Key: {key} | New Record: {isNewRecord} | Score: {score}");

            if (isNewRecord)
            {
                // [Visual Note] Lookup ID from SO
                string id = leaderboardConfig.GetLeaderboardId(gameData.GameMode, gameData.IsMultiplayerMode, gameData.SelectedIntensity.Value);
                if(!string.IsNullOrEmpty(id)) SubmitScoreToLeaderboard(id, score);
            }

            SaveProfile();
        }

        #endregion

        #region Public API - Hex Race (Single Player)

        public float GetBestRaceTime()
        {
            if (!_isReady) return 0f;
            string key = GetCurrentModeAndIntensityKey();

            if (_cachedProfile.HexRaceStats.BestRaceTimes.TryGetValue(key, out float time))
            {
                return time;
            }
            return 0f;
        }

        public void ReportHexRaceStats(int cleanCrystals, float maxDrift, float maxBoost, float raceTime)
        {
            if (!_isReady) return;

            _cachedProfile.HexRaceStats.TotalCleanCrystalsCollected += cleanCrystals;
            _cachedProfile.HexRaceStats.TotalDriftTime += maxDrift;
            _cachedProfile.TotalGamesPlayed++;

            if (maxDrift > _cachedProfile.HexRaceStats.LongestSingleDrift)
                _cachedProfile.HexRaceStats.LongestSingleDrift = maxDrift;
            if (maxBoost > _cachedProfile.HexRaceStats.MaxTimeAtHighBoost)
                _cachedProfile.HexRaceStats.MaxTimeAtHighBoost = maxBoost;

            string key = GetCurrentModeAndIntensityKey();

            bool isNewRecord = false;
            if (raceTime < 10000f)
            {
                isNewRecord = _cachedProfile.HexRaceStats.TryUpdateBestTime(key, raceTime);
            }

            Debug.Log($"[StatsManager] Hex Race Stats. Key: {key} | New Best: {isNewRecord} | Time: {raceTime}");

            if (isNewRecord && raceTime < 10000f)
            {
                string id = leaderboardConfig.GetLeaderboardId(gameData.GameMode, false, gameData.SelectedIntensity.Value);
                if(!string.IsNullOrEmpty(id)) SubmitScoreToLeaderboard(id, raceTime);
            }

            SaveProfile();
        }

        #endregion

        #region Public API - Hex Race (Multiplayer)
        
        public void ReportMultiplayerHexStats(int clean, float drift, float boost, int jousts, float score)
        {
            if (!_isReady) return;

            _cachedProfile.MultiHexStats.TotalCleanCrystalsCollected += clean;
            _cachedProfile.MultiHexStats.TotalDriftTime += drift;
            _cachedProfile.MultiHexStats.TotalJoustsWon += jousts;
            _cachedProfile.TotalGamesPlayed++;

            if (drift > _cachedProfile.MultiHexStats.TotalDriftTime) 
                _cachedProfile.MultiHexStats.TotalDriftTime = drift;

            if (score < 10000f)
            {
                string key = GetCurrentModeAndIntensityKey(); 
                
                bool isNewRecord = _cachedProfile.MultiHexStats.TryUpdateBestTime(key, score);
                _cachedProfile.MultiHexStats.TotalWins++;

                if (isNewRecord)
                {
                    string id = leaderboardConfig.GetLeaderboardId(gameData.GameMode, true, gameData.SelectedIntensity.Value);
                    if(!string.IsNullOrEmpty(id)) SubmitScoreToLeaderboard(id, score);
                }
            }
            SaveProfile();
        }

        #endregion

        #region General & Helpers

        public void TrackPlayAgain()
        {
            if (!_isReady) return;
            _cachedProfile.TotalPlayAgainPressed++;
            SaveProfile();
        }
        
        string GetCurrentModeAndIntensityKey()
        {
            string typeSuffix = gameData.IsMultiplayerMode ? "MP" : "SP";
            return $"{gameData.GameMode}_{typeSuffix}_{gameData.SelectedIntensity.Value}";
        }

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
                if (!data.TryGetValue(CLOUD_KEY, out var item)) return;
                _cachedProfile = item.Value.GetAs<PlayerStatsProfile>();
    
                _cachedProfile.BlitzStats ??= new WildlifeBlitzPlayerStatsProfile();
                _cachedProfile.HexRaceStats ??= new HexRacePlayerStatsProfile();
                _cachedProfile.MultiHexStats ??= new MultiplayerHexRacePlayerStatsProfile();

                Debug.Log($"[StatsManager] Profile Loaded.");
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
            }
            catch (Exception e)
            {
                Debug.LogError($"[StatsManager] Save Failed: {e.Message}");
            }
        }

        #endregion
    }
}