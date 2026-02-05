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

        [Header("Config")] 
        [SerializeField] GameDataSO gameData;
        
        [SerializeField] string blitzLeaderboardId = "wildlife_blitz_highscore"; 

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
            catch (Exception e)
            {
                // TODO handle exception
            }
        }

        #region Public API

        public int GetHighScoreForCurrentMode()
        {
            if (!_isReady) return 0;
            string key = GetCurrentModeKey();
            return _cachedProfile.HighScores.TryGetValue(key, out int score) ? score : 0;
        }

        public void ReportMatchStats(int crystals, int lifeForms, int score)
        {
            if (!_isReady) return;

            // 1. Update Accumulative
            _cachedProfile.LifetimeCrystalsCollected += crystals;
            _cachedProfile.LifetimeLifeFormsKilled += lifeForms;
            _cachedProfile.TotalGamesPlayed++;

            // 2. High Score Logic
            string key = GetCurrentModeKey();
            bool isNewRecord = _cachedProfile.TryUpdateHighScore(key, score);

            Debug.Log($"[StatsManager] Match Stats Reported. New Record: {isNewRecord} | Score: {score}");

            // 3. Leaderboard & Save
            if (isNewRecord)
            {
                SubmitScoreToLeaderboard(score);
                Debug.Log($"<color=yellow>[StatsManager] New High Score! {score}</color>");
            }
            
            // Force save immediately after match update
            SaveProfile();
        }
        
        // Helper for Analytics Event (Called by Scoreboard)
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
            Debug.Log("[StatsManager] Play Again Analytics Sent");
            
            SaveProfile();
        }

        #endregion

        #region Helpers
        
        string GetCurrentModeKey()
        {
            string typeSuffix = gameData.IsMultiplayerMode ? "MP" : "SP";
            return $"{gameData.GameMode}_{gameData.SelectedIntensity.Value}_{typeSuffix}";
        }

        async void SubmitScoreToLeaderboard(int score)
        {
            try
            {
                await LeaderboardsService.Instance.AddPlayerScoreAsync(blitzLeaderboardId, score);
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
                    Debug.Log($"[StatsManager] Profile Loaded. Games Played: {_cachedProfile.TotalGamesPlayed}");
                }
            }
            catch { /* New user */ }
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