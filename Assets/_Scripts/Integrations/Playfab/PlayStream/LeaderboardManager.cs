using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Integrations.Playfab.Authentication;
using PlayFab;
using PlayFab.ClientModels;
using CosmicShore.Utility.Singleton;
using UnityEngine;

namespace CosmicShore.Integrations.Playfab.PlayStream
{
    /// <summary>
    /// Leaderboard Manager
    /// Handles online and offline leaderboard stats
    /// </summary>
    public class LeaderboardManager : SingletonPersistent<LeaderboardManager>
    {
        /// <summary>
        /// Leaderboard Entry struct
        /// Has members: Position(Rank), Score and Player Display Name
        /// </summary>
        [System.Serializable]
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
        readonly string OfflineStatsFileName = "offline_stats.data";
        // Local storage data prefix
        readonly string CachedLeaderboardFileNamePrefix = "leaderboard_";

        bool online = false;

        /// <summary>
        /// Come Online
        /// Turn Online status on, upload and clear local leaderboard stats.
        /// </summary>
        void ComeOnline()
        {
            Debug.Log("LeaderboardManager - ComeOnline");
            online = true;
            ReportAndFlushOfflineStatistics();
        }

        /// <summary>
        /// Go Offline
        /// Turn Online status off
        /// </summary>
        void GoOffline()
        {
            Debug.Log("LeaderboardManager - GoOffline");
            online = false;
        }

        /// <summary>
        /// On Enabling Leaderboard Manager
        /// Register network status detection and local data upload delegates
        /// </summary>
        void OnEnable()
        {
            NetworkMonitor.NetworkConnectionFound += ComeOnline;
            NetworkMonitor.NetworkConnectionLost += GoOffline;
            AuthenticationManager.OnProfileLoaded += ReportAndFlushOfflineStatistics;
        }

        /// <summary>
        /// On Disable Leaderboard Manager
        /// Unregister network status detection and local data upload delegates
        /// </summary>
        void OnDisable()
        {
            NetworkMonitor.NetworkConnectionFound -= ComeOnline;
            NetworkMonitor.NetworkConnectionLost -= GoOffline;
            AuthenticationManager.OnProfileLoaded -= ReportAndFlushOfflineStatistics;
        }

        /// <summary>
        /// Report and Flush Offline Stats Wrapper
        /// Start local data uploading and clearing coroutine 
        /// </summary>
        void ReportAndFlushOfflineStatistics()
        {
            StartCoroutine(ReportAndFlushStatisticsCoroutine());
        }

        /// <summary>
        /// Report and Flush Offline Stats Coroutine
        /// Local data uploading and clearing coroutine logic 
        /// </summary>
        IEnumerator ReportAndFlushStatisticsCoroutine()
        {
            yield return new WaitUntil(() => AuthenticationManager.PlayFabAccount != null);

            Debug.Log("LeaderboardManager - ReportAndFlushOfflineStatistics");
            var dataAccessor = new DataAccessor(OfflineStatsFileName);
            var offlineStatistics = dataAccessor.Load<List<StatisticUpdate>>();

            if (offlineStatistics.Count > 0)
            {
                Debug.Log($"LeaderboardManager - StatCount:{offlineStatistics.Count}");
                UpdatePlayerStatistic(offlineStatistics);
                dataAccessor.Flush();
            }
        }

        /// <summary>
        /// Update Gameplay Stats
        /// Upload game mode, ship type, intensity level and scores to memory
        /// </summary>
        public void ReportGameplayStatistic(MiniGames gameMode, ShipTypes shipType, int intensity, int score, bool golfScoring)
        {
            // Build list of statistics to update
            // One entry for each score for specific game mode/ship combination
            // One entry for each score for game mode any ship
            // One entry to count how many times people have played a given game with a given ship

            // Playfab does not support reverse sort for leaderboards... take the negative to figure out the position, then flip it again when displaying the score
            if (golfScoring)
                score *= -1;

            Debug.Log($"UpdateGameplayStats - gameMode:{gameMode}, shipType:{shipType}, intensity:{intensity}, score:{score}");
            List<StatisticUpdate> stats = new()
            {
                new StatisticUpdate()
                {
                    StatisticName = GetGameplayStatKey(gameMode, shipType),
                    Value = score
                },
                new StatisticUpdate()
                {
                    StatisticName = GetGameplayStatKey(gameMode, ShipTypes.Any),
                    Value = score
                },
                new StatisticUpdate()
                {
                    StatisticName = GetGameplayStatKey(gameMode, shipType) + "_PlayCount",
                    Value = 1
                }
            };

            ReportPlayerStatistic(stats, new Dictionary<string, string>() { { "Intensity", intensity.ToString() } });
        }

        /// <summary>
        /// Get Gameplay Stats Key
        /// Combines game mode and ship type as search key, and return it.
        /// </summary>
        public string GetGameplayStatKey(MiniGames gameMode, ShipTypes shipType)
        {
            var statKey = gameMode.ToString().ToUpper() + "_" + shipType.ToString().ToUpper();

            Debug.Log("GetGameplayStatKey: " +  statKey);

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
            if (online)
            {
                Debug.Log($"LeaderboardManager.UpdatePlayerStatistic - online");
                customTags.Add("BuildNumber", Application.buildGUID);

                var request = new UpdatePlayerStatisticsRequest();
                request.AuthenticationContext = AuthenticationManager.PlayFabAccount.AuthContext;
                request.CustomTags = customTags;
                request.Statistics = stats;
                
                PlayFabClientAPI.UpdatePlayerStatistics(
                    request,
                    response =>
                    {
                        Debug.Log("UpdatePlayerStatistic success: " + response.ToString());
                    },
                    error =>
                    {
                        Debug.Log("UpdatePlayerStatistic failure: " + error.GenerateErrorReport());
                    }
                );
            }
            else
            {
                Debug.Log($"LeaderboardManager.UpdatePlayerStatistic - offline");
                // TODO: custom tags lost?
                var dataAccessor = new DataAccessor(OfflineStatsFileName);
                var offlineStatistics = dataAccessor.Load<List<StatisticUpdate>>();
                offlineStatistics.AddRange(stats);
                dataAccessor.Save(offlineStatistics);
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
            if (online)
            {
                var request = new GetLeaderboardAroundPlayerRequest();
                request.AuthenticationContext = AuthenticationManager.PlayFabAccount.AuthContext;
                request.StatisticName = leaderboardName;
                request.CustomTags = customTags;
                request.ProfileConstraints = new PlayerProfileViewConstraints()
                {
                    ShowDisplayName = true,
                    ShowAvatarUrl = true
                };
                
                PlayFabClientAPI.GetLeaderboardAroundPlayer(
                    request,
                    response =>
                    {
                        var entries = response.Leaderboard
                            .Select(entry => new LeaderboardEntry(
                                entry.Profile.DisplayName, 
                                entry.PlayFabId, 
                                entry.StatValue, 
                                entry.Position,
                                entry.Profile.AvatarUrl))
                            .ToList();

                        callback(entries);

                        var dataAccessor = new DataAccessor(GetLeaderboardFileName(leaderboardName));
                        dataAccessor.Save(entries);

                        Debug.Log("UpdatePlayerStatistic success: " + response);
                    },
                    error => Debug.Log("UpdatePlayerStatistic failure: " + error.GenerateErrorReport()));
            }
            else
            {
                var dataAccessor = new DataAccessor(GetLeaderboardFileName(leaderboardName));
                var cachedLeaderboard = dataAccessor.Load<List<LeaderboardEntry>>();
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
        /// Fetches leaderboard data by name (aggregation of mini game and ship type name)
        /// Takes front end leaderboard name and callback
        /// Might be good to add error handler
        /// </summary>
        public void RequestLeaderboard(string leaderboardName, LoadLeaderboardCallBack callback)
        {
            var request = new GetLeaderboardRequest();
            request.StatisticName = leaderboardName;
            request.StartPosition = 0;
            request.MaxResultsCount = 10;
            request.ProfileConstraints = new PlayerProfileViewConstraints
            {
                ShowDisplayName = true,
                ShowAvatarUrl = true
            };
            PlayFabClientAPI.GetLeaderboard(
                request,
                result => HandleLeaderboardData(result, callback),
                error => Debug.Log(error.GenerateErrorReport())
                );
        }

        /// <summary>
        /// Handle Leaderboard Data
        /// For now displaying leaderboard data in the console, let callback handle leaderboard data
        /// </summary>
        void HandleLeaderboardData(GetLeaderboardResult result, LoadLeaderboardCallBack callback)
        {
            // result null check, nothing to display
            if (result == null)
                return;
        
            // The result doesn't return with leaderboard name, BLOCKBANDIT_ANY is a placeholder
            Debug.Log($"Leaderboard Manger - BLOCKBANDIT_ANY");
            // Store relevant data in leaderboard entry struct
            var leaderboardEntry = new List<LeaderboardEntry>();
            foreach (var entry in result.Leaderboard)
            {
                Debug.Log($"Leaderboard Manager - BLOCKBANDIT_ANY display name: {entry.DisplayName} score: {entry.StatValue.ToString()} position: {entry.Position.ToString()}");
                leaderboardEntry.Add(new LeaderboardEntry(entry.DisplayName, entry.PlayFabId, entry.StatValue, entry.Position, entry.Profile.AvatarUrl));
            }
            // Let callback handle leaderboard data
            callback(leaderboardEntry);
            Debug.Log($"Leaderboard Manager - BLOCKBANDIT_ANY board version: {result.Version.ToString()}");
        }

        #endregion

        #region Request Friend Leaderboard

        /// <summary>
        /// Request Friend Leaderboard By leaderboard name
        /// Fetches friend leaderboard data by name (aggregation of mini game and ship type name)
        /// Takes front end leaderboard name and callback
        /// Might be good to add error handler
        /// </summary>
        public void RequestFriendLeaderboard(string leaderboardName, LoadLeaderboardCallBack callback)
        {
            PlayFabClientAPI.GetFriendLeaderboard(
                new GetFriendLeaderboardRequest
                {
                    StatisticName = leaderboardName,
                    // Start position is required in request friend leaderboard request
                    StartPosition = 0,
                    // Not required, set default as 20 for now
                    MaxResultsCount = 20,
                }, (result) => HandleLeaderboardData(result, callback),
                (error) =>
                {
                    // TODO: add error handler
                    Debug.Log(error.GenerateErrorReport());
                }
            );
        }

        #endregion
    
    }
}