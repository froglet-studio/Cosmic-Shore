using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Services.Auth;
using Unity.Services.CloudSave;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace CosmicShore.Game.Analytics
{
    public class UGSStatsManager : MonoBehaviour
    {
        public static UGSStatsManager Instance { get; private set; }

        [Header("Dependencies")] 
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
                    await Task.Delay(500);

                _isReady = true;
                LoadProfile();
            }
            catch (Exception) { /* Fail silently */ }
        }

        #region Public API - Smart High Score Evaluation

        /// <summary>
        /// Returns the "Best" score between the Cloud Cache and the Current Session Score.
        /// Automatically handles logic for Modes where "Lower is Better" (Hex) vs "Higher is Better" (Blitz).
        /// </summary>
        public float GetEvaluatedHighScore(GameModes mode, int intensity, float currentSessionScore)
        {
            if (!_isReady) return currentSessionScore;

            string key = $"{mode}_{intensity}";

            if (mode is GameModes.HexRace or GameModes.MultiplayerHexRaceGame)
            {
                float cloudBest = _cachedProfile.HexRaceStats.BestRaceTimes.GetValueOrDefault(key, 0f);
                if (cloudBest <= 0.001f) return currentSessionScore;
                return currentSessionScore >= 10000f ? cloudBest : Mathf.Min(cloudBest, currentSessionScore);
            }
            else 
            {
                int cloudBest = _cachedProfile.BlitzStats.HighScores.GetValueOrDefault(key, 0);
                return Mathf.Max(cloudBest, currentSessionScore);
            }
        }

        #endregion

        #region Public API - Reporting

        public void ReportBlitzStats(GameModes mode, int intensity, int crystals, int lifeForms, int score)
        {
            if (!_isReady) return;

            _cachedProfile.BlitzStats.LifetimeCrystalsCollected += crystals;
            _cachedProfile.BlitzStats.LifetimeLifeFormsKilled += lifeForms;
            _cachedProfile.TotalGamesPlayed++;

            string key = $"{mode}_{intensity}";
            _cachedProfile.BlitzStats.TryUpdateHighScore(key, score);

            SubmitScoreInternal(mode, intensity, score);
            SaveProfile();
        }

        public void ReportHexRaceStats(GameModes mode, int intensity, int cleanCrystals, float maxDrift, float maxBoost, float raceTime)
        {
            if (!_isReady) return;

            _cachedProfile.HexRaceStats.TotalCleanCrystalsCollected += cleanCrystals;
            _cachedProfile.HexRaceStats.TotalDriftTime += maxDrift;
            _cachedProfile.TotalGamesPlayed++;

            if (maxDrift > _cachedProfile.HexRaceStats.LongestSingleDrift) _cachedProfile.HexRaceStats.LongestSingleDrift = maxDrift;
            if (maxBoost > _cachedProfile.HexRaceStats.MaxTimeAtHighBoost) _cachedProfile.HexRaceStats.MaxTimeAtHighBoost = maxBoost;

            if (raceTime < 10000f)
            {
                string key = $"{mode}_{intensity}";
                _cachedProfile.HexRaceStats.TryUpdateBestTime(key, raceTime);
                SubmitScoreInternal(mode, intensity, raceTime);
            }
            SaveProfile();
        }

        public void ReportMultiplayerHexStats(GameModes mode, int intensity, int clean, float drift, int jousts, float score)
        {
            if (!_isReady) return;

            _cachedProfile.MultiHexStats.TotalCleanCrystalsCollected += clean;
            _cachedProfile.MultiHexStats.TotalDriftTime += drift; 
            _cachedProfile.MultiHexStats.TotalJoustsWon += jousts;
            _cachedProfile.TotalGamesPlayed++;

            if (drift > _cachedProfile.MultiHexStats.LongestSingleDrift) 
                _cachedProfile.MultiHexStats.LongestSingleDrift = drift;

            string key = $"{mode}_{intensity}";
            if (score < 10000f)
            {
                _cachedProfile.MultiHexStats.TryUpdateBestTime(key, score);
                _cachedProfile.MultiHexStats.TotalWins++;

                SubmitScoreInternal(mode, intensity, score);
            }
    
            SaveProfile();
        }

        #endregion

        #region Internal

        public void TrackPlayAgain()
        {
            if (!_isReady) return;
            _cachedProfile.TotalPlayAgainPressed++;
            SaveProfile();
        }

        async void SubmitScoreInternal(GameModes mode, int intensity, double score)
        {
            try
            {
                string id = leaderboardConfig.GetLeaderboardId(mode, intensity);
                if (string.IsNullOrEmpty(id)) return;

                try { await LeaderboardsService.Instance.AddPlayerScoreAsync(id, score); }
                catch (Exception e) { Debug.LogWarning($"[Stats] Upload Failed: {e.Message}"); }
            }
            catch (Exception e)
            {
                // TODO handle exception
            }
        }

        async void LoadProfile()
        {
            try
            {
                var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CLOUD_KEY });
                if (data.TryGetValue(CLOUD_KEY, out var item)) _cachedProfile = item.Value.GetAs<PlayerStatsProfile>();
                
                _cachedProfile.BlitzStats ??= new WildlifeBlitzPlayerStatsProfile();
                _cachedProfile.HexRaceStats ??= new HexRacePlayerStatsProfile();
                _cachedProfile.MultiHexStats ??= new MultiplayerHexRacePlayerStatsProfile();
            }
            catch { /* New Profile */ }
        }

        async void SaveProfile()
        {
            try
            {
                _cachedProfile.LastLoginTick = DateTime.UtcNow.Ticks;
                var data = new Dictionary<string, object> { { CLOUD_KEY, _cachedProfile } };
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            }
            catch (Exception e) { Debug.LogError($"[Stats] Save Failed: {e.Message}"); }
        }

        #endregion
    }
}