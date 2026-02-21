using System;
using System.Collections.Generic;
using CosmicShore.Utilities;
using Unity.Services.CloudSave;
using Unity.Services.Leaderboards;
using UnityEngine;

namespace CosmicShore.Game.Analytics
{
    public class UGSStatsManager : SingletonPersistent<UGSStatsManager>
    {
        [Header("Dependencies")] 
        [SerializeField] LeaderboardConfigSO leaderboardConfig; 
        
        [SerializeField]
        AuthenticationDataVariable authenticationDataVariable;
        AuthenticationData authenticationData => authenticationDataVariable.Value;

        private PlayerStatsProfile _cachedProfile = new PlayerStatsProfile();
        private const string CLOUD_KEY = "PLAYER_STATS_PROFILE";
        private bool _isReady = false;

        private void OnEnable()
        {
            authenticationData.OnSignedIn.OnRaised += OnAuthenticationSignedIn;
        }

        private void OnDisable()
        {
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

            _cachedProfile.BlitzStats.LifetimeCrystalsCollected += crystals;
            _cachedProfile.BlitzStats.LifetimeLifeFormsKilled += lifeForms;
            _cachedProfile.TotalGamesPlayed++;

            string key = $"{mode}_{intensity}";
            _cachedProfile.BlitzStats.TryUpdateHighScore(key, score);

            SubmitScoreInternal(mode, intensity, score);
            SaveProfile();
        }

        public void ReportHexRaceStats(GameModes mode, int intensity, int clean, float drift, int jousts, float score)
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

        public void ReportJoustStats(GameModes mode, int intensity, int joustsWon, float raceTime)
        {
            if (!_isReady) return;

            _cachedProfile.JoustStats.TotalJoustsWon += joustsWon;
            _cachedProfile.TotalGamesPlayed++;

            string key = $"{mode}_{intensity}";
            
            if (raceTime < 10000f)
            {
                _cachedProfile.JoustStats.TryUpdateBestTime(key, raceTime);
                _cachedProfile.JoustStats.TotalWins++;
                SubmitScoreInternal(mode, intensity, raceTime);
            }
    
            SaveProfile();
        }

        public void ReportCrystalCaptureStats(GameModes mode, int intensity, int crystals)
        {
            if (!_isReady) return;

            _cachedProfile.CrystalCaptureStats.LifetimeCrystalsCollected += crystals;
            _cachedProfile.CrystalCaptureStats.TotalWins++;
            _cachedProfile.TotalGamesPlayed++;

            string key = $"{mode}_{intensity}";
            _cachedProfile.CrystalCaptureStats.TryUpdateHighScore(key, crystals);

            SubmitScoreInternal(mode, intensity, crystals);
            SaveProfile();
        }
        
        public void TrackPlayAgain()
        {
            if (!_isReady) return;
            _cachedProfile.TotalPlayAgainPressed++;
            SaveProfile();
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
                if (string.IsNullOrEmpty(id)) return;

                try { await LeaderboardsService.Instance.AddPlayerScoreAsync(id, score); }
                catch (Exception e) { Debug.LogWarning($"[Stats] Upload Failed: {e.Message}"); }
            }
            catch (Exception)
            {
            }
        }

        async void LoadProfile()
        {
            try
            {
                var data = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CLOUD_KEY });
                if (data.TryGetValue(CLOUD_KEY, out var item)) 
                    _cachedProfile = item.Value.GetAs<PlayerStatsProfile>();
                
                _cachedProfile.BlitzStats ??= new WildlifeBlitzPlayerStatsProfile();
                _cachedProfile.MultiHexStats ??= new HexRacePlayerStatsProfile();
                _cachedProfile.JoustStats ??= new JoustPlayerStatsProfile();
                _cachedProfile.CrystalCaptureStats ??= new CrystalCapturePlayerStatsProfile();
            }
            catch { }
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