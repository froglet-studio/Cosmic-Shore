// Ported from Assets/_Scripts/System/Playfab/PlayStream/LeaderboardManager.cs
// (Leaderboards unit 2026-07-10) — structure verbatim. Upstream this manager is in its
// "[PLAYFAB DISABLED]" state (Start no longer wires the network events; UGS
// UGSStatsManager owns live leaderboards; pending removal) — so its LIVE behavior is
// the OFFLINE lane: stats accumulate into offline_stats.data and leaderboard fetches
// serve the DataAccessor-cached lists. That lane is REAL here. The PlayFab online
// lanes (PlayFabClientAPI.*) have no engine SDK and are deviation-commented in place;
// `_online` stays false exactly like the disabled upstream. PlayFab.ClientModels.
// StatisticUpdate is carried as the minimal data type below (same serialized shape).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.Utility;
using CosmicShore.Engine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.Core
{
    // PORT Deviation (Leaderboards unit): PlayFab.ClientModels.StatisticUpdate — the
    // serialized shape the offline lane persists (StatisticName + Value).
    [Serializable]
    public class StatisticUpdate
    {
        public string StatisticName;
        public int Value;
    }

    /// <summary>
    /// Leaderboard Manager
    /// Handles online and offline leaderboard stats
    /// </summary>
    public class LeaderboardManager : SingletonPersistent<LeaderboardManager>
    {
        [SerializeField]
        NetworkMonitorDataVariable  _networkMonitorDataVariable;
        NetworkMonitorData _networkMonitorData => _networkMonitorDataVariable.Value;

        /// <summary>
        /// Leaderboard Entry struct
        /// Has members: Position(Rank), Score and Player Display Name
        /// </summary>
        [Serializable]
        public struct LeaderboardEntry
        {
            public int Position;
            public int Score;
            public string DisplayName;
            public string PlayerId;
            public string AvatarUrl;

            public LeaderboardEntry(string displayName, string playerId, int score, int position, string avatarUrl)
            {
                DisplayName = displayName;
                PlayerId = playerId;
                Score = score;
                Position = position;
                AvatarUrl = avatarUrl;
            }
        }

        // Offline local data file name
        private const string OfflineStatsFileName = "offline_stats.data";

        // Local storage data prefix
        private const string CachedLeaderboardFileNamePrefix = "leaderboard_";
        public const string DailyChallengeStatisticName = "DAILY_CHALLENGE";

        bool _online;

        private void Start()
        {
            // [PLAYFAB DISABLED] Leaderboards now handled by UGS UGSStatsManager. Pending removal.
        }

        /// <summary>
        /// Clear out all delegates
        /// </summary>
        private void OnDestroy()
        {
            _networkMonitorData.OnNetworkFound.OnRaised -= ComeOnline;
            _networkMonitorData.OnNetworkLost.OnRaised -= GoOffline;
            PlayerDataController.OnProfileLoaded -= ReportAndFlushOfflineStatistics;
            this.LogWithClassMethod(MethodBase.GetCurrentMethod()?.Name, "this instance is disposed.");
        }

        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Come Online
        /// Turn Online status on, upload and clear local leaderboard stats.
        /// </summary>
        void ComeOnline()
        {
            CSDebug.Log("LeaderboardManager - ComeOnline");
            _online = true;
            ReportAndFlushOfflineStatistics();
        }

        /// <summary>
        /// Go Offline
        /// Turn Online status off
        /// </summary>
        void GoOffline()
        {
            CSDebug.Log("LeaderboardManager - GoOffline");
            _online = false;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void ReportAndFlushOfflineStatistics()
        {
            StartCoroutine(ReportAndFlushStatisticsCoroutine());
        }

        /// <summary>
        /// Report and Flush Offline Stats Coroutine
        /// Local data uploading and clearing coroutine logic
        /// </summary>
        private IEnumerator ReportAndFlushStatisticsCoroutine()
        {
            yield return new WaitUntil(() => AuthenticationManager.PlayFabAccount != null);

            CSDebug.Log("LeaderboardManager - ReportAndFlushOfflineStatistics");
            var offlineStatistics = DataAccessor.Load<List<StatisticUpdate>>(OfflineStatsFileName);

            if (offlineStatistics.Count > 0)
            {
                CSDebug.Log($"LeaderboardManager - StatCount:{offlineStatistics.Count}");
                UpdatePlayerStatistic(offlineStatistics);
                DataAccessor.Flush(OfflineStatsFileName);
            }
        }

        private async void WaitForPlayFabAccountAsync()
        {
            while (AuthenticationManager.PlayFabAccount == null)
            {
                await Task.Delay(100);
            }
        }

        /// <summary>
        /// Update Gameplay Stats
        /// Upload game mode, vessel type, intensity level and scores to memory
        /// </summary>
        public void ReportGameplayStatistic(GameModes gameMode, VesselClassType vesselType, int intensity, int score, bool golfScoring)
        {
            // Build list of statistics to update
            // One entry for each Score for specific game mode/vessel combination
            // One entry for each Score for game mode any vessel
            // One entry to count how many times people have played a given game with a given vessel

            // Playfab does not support reverse sort for leaderboards... take the negative to figure out the position, then flip it again when displaying the Score
            if (golfScoring)
                score *= -1;

            CSDebug.Log($"UpdateGameplayStats - gameMode:{gameMode}, shipType:{vesselType}, intensity:{intensity}, Score:{score}");
            List<StatisticUpdate> stats = new()
            {
                new StatisticUpdate()
                {
                    StatisticName = GetGameplayStatKey(gameMode, vesselType),
                    Value = score
                },
                new StatisticUpdate()
                {
                    StatisticName = GetGameplayStatKey(gameMode, VesselClassType.Any),
                    Value = score
                },
                new StatisticUpdate()
                {
                    StatisticName = GetGameplayStatKey(gameMode, vesselType) + "_PlayCount",
                    Value = 1
                }
            };

            ReportPlayerStatistic(stats, new Dictionary<string, string>() { { "Intensity", intensity.ToString() } });
        }

        /// <summary>
        /// Update Gameplay Stats
        /// Upload game mode, vessel type, intensity level and scores to memory
        /// </summary>
        public void ReportDailyChallengeStatistic(int score, bool golfScoring)
        {
            // Playfab does not support reverse sort for leaderboards... take the negative to figure out the position, then flip it again when displaying the Score
            if (golfScoring)
                score *= -1;

            CSDebug.Log($"ReportDailyChallengeStatistic - Score:{score}");
            List<StatisticUpdate> stats = new()
            {
                new StatisticUpdate()
                {
                    StatisticName = DailyChallengeStatisticName,
                    Value = score
                }
            };

            ReportPlayerStatistic(stats, new Dictionary<string, string>());
        }


        /// <summary>
        /// Get Gameplay Stats Key
        /// Combines game mode and vessel type as search key, and return it.
        /// </summary>
        public string GetGameplayStatKey(GameModes gameMode, VesselClassType vesselType)
        {
            var statKey = gameMode.ToString().ToUpper() + "_" + vesselType.ToString().ToUpper();

            CSDebug.Log("GetGameplayStatKey: " +  statKey);

            return statKey;
        }

        /// <summary>
        /// Update Player Stats - First Time
        /// Update player stats when first time populating a new dictionary.
        /// </summary>
        void UpdatePlayerStatistic(List<StatisticUpdate> stats)
        {
            ReportPlayerStatistic(stats, new());
        }

        /// <summary>
        /// Update Player Stats - Aggregate
        /// Update player stats to an existing dictionary.
        /// </summary>
        void ReportPlayerStatistic(List<StatisticUpdate> stats, Dictionary<string, string> customTags)
        {
            if (_online)
            {
                CSDebug.Log($"LeaderboardManager.UpdatePlayerStatistic - online");
                // PORT Deviation (Leaderboards unit, PlayFab SDK): the online lane built an
                // UpdatePlayerStatisticsRequest (auth context + BuildNumber tag) and called
                // PlayFabClientAPI.UpdatePlayerStatistics with success/failure logs. No
                // engine SDK — and `_online` is never true while upstream keeps the
                // "[PLAYFAB DISABLED]" Start, so this lane is unreachable there too.
            }
            else
            {
                CSDebug.Log($"LeaderboardManager.UpdatePlayerStatistic - offline");
                // TODO: custom tags lost?
                var offlineStatistics = DataAccessor.Load<List<StatisticUpdate>>(OfflineStatsFileName);
                offlineStatistics.AddRange(stats);
                DataAccessor.Save(OfflineStatsFileName, offlineStatistics);
            }
        }

        /// <summary>
        /// Load Leaderboard callback delegate
        /// Handles newly added leaderboard stats
        /// </summary>
        public delegate void LoadLeaderboardCallBack(List<LeaderboardEntry> entries);

        /// <summary>
        /// Fetch Leaderboard Stats - First Time
        /// Add new entries to a leaderboard and offer data handler
        /// </summary>
        public void FetchLeaderboard(string leaderboardName, LoadLeaderboardCallBack callback)
        {
            FetchLeaderboard(leaderboardName, new(), callback);
        }

        /// <summary>
        /// Fetch Leaderboard Stats - Aggregate
        /// Add stats in memory to leaderboard and offer data handler
        /// </summary>
        public void FetchLeaderboard(string leaderboardName, Dictionary<string, string> customTags, LoadLeaderboardCallBack callback)
        {
            if (_online)
            {
                // PORT Deviation (Leaderboards unit, PlayFab SDK): the online lane called
                // PlayFabClientAPI.GetLeaderboardAroundPlayer (display-name + avatar
                // constraints), mapped the response into LeaderboardEntry rows, invoked the
                // callback, and re-cached via DataAccessor.Save. No engine SDK — and the
                // lane is unreachable while `_online` stays false (disabled upstream too).
            }
            else
            {
                var cachedLeaderboard = DataAccessor.Load<List<LeaderboardEntry>>(GetLeaderboardFileName(leaderboardName));
                callback(cachedLeaderboard);
            }
        }

        /// <summary>
        /// Get Leaderboard File Name
        /// Takes leaderboard Name and return leaderboard data file in local storage.
        /// </summary>
        string GetLeaderboardFileName(string leaderboardName)
        {
            return CachedLeaderboardFileNamePrefix + leaderboardName + ".data";
        }

        #region Request Leaderboard

        /// <summary>
        /// Get Leaderboard By leaderboard name
        /// Fetches leaderboard data by name (aggregation of mini game and vessel type name)
        /// Takes front end leaderboard name and callback
        /// Might be good to add error handler
        /// </summary>
        public void RequestLeaderboard(string leaderboardName, LoadLeaderboardCallBack callback)
        {
            // PORT Deviation (Leaderboards unit, PlayFab SDK): PlayFabClientAPI.GetLeaderboard
            // (top 10, display-name + avatar constraints) → HandleLeaderboardData. No engine
            // SDK; like an unanswered PlayFab request, the callback never fires.
            CSDebug.Log($"LeaderboardManager.RequestLeaderboard({leaderboardName}) - PlayFab SDK not present (legacy lane)");
        }

        #endregion

        #region Request Friend Leaderboard

        /// <summary>
        /// Request Friend Leaderboard By leaderboard name
        /// Fetches friend leaderboard data by name (aggregation of mini game and vessel type name)
        /// Takes front end leaderboard name and callback
        /// Might be good to add error handler
        /// </summary>
        public void RequestFriendLeaderboard(string leaderboardName, LoadLeaderboardCallBack callback)
        {
            // PORT Deviation (Leaderboards unit, PlayFab SDK): PlayFabClientAPI.
            // GetFriendLeaderboard (top 20) → HandleLeaderboardData. No engine SDK; like an
            // unanswered PlayFab request, the callback never fires.
            CSDebug.Log($"LeaderboardManager.RequestFriendLeaderboard({leaderboardName}) - PlayFab SDK not present (legacy lane)");
        }

        #endregion

    }
}
