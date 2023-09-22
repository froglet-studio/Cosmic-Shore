using _Scripts._Core.Playfab_Models;
using PlayFab;
using PlayFab.ClientModels;
using StarWriter.Utility.Singleton;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

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
    public struct LeaderboardEntryV2
    {
        public int Position;
        public int Score;
        public string DisplayName;

        public LeaderboardEntryV2(string displayName, int score, int position)
        {
            DisplayName = displayName;
            Score = score;
            Position = position;
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
        yield return new WaitUntil(() => AuthenticationManager.PlayerAccount != null);

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
    public void UpdateGameplayStatistic(MiniGames gameMode, ShipTypes shipType, int intensity, List<int> scores)
    {
        // Build list of statistics to update
        // One entry for each score for specific game mode/ship combination
        // One entry for each score for game mode any ship

        Debug.Log($"UpdateGameplayStats - gameMode:{gameMode}, shipType:{shipType}, intensity:{intensity}, score0:{scores[0]}");
        List<StatisticUpdate> stats = new List<StatisticUpdate>();

        foreach (var score in scores)
        {
            stats.Add(new StatisticUpdate() {
                StatisticName = GetGameplayStatKey(gameMode, shipType),
                Value = score
            });

            stats.Add(new StatisticUpdate()
            {
                StatisticName = GetGameplayStatKey(gameMode, ShipTypes.Any),
                Value = score
            });
        }
        UpdatePlayerStatistic(stats, new Dictionary<string, string>() { { "Intensity", intensity.ToString() } });
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
        UpdatePlayerStatistic(stats, new());
    }

    /// <summary>
    /// Update Player Stats - Aggregate
    /// Update player stats to an existing dictionary.
    /// </summary>
    void UpdatePlayerStatistic(List<StatisticUpdate> stats, Dictionary<string, string> customTags)
    {
        if (online)
        {
            Debug.Log($"LeaderboardManager.UpdatePlayerStatistic - online");
            customTags.Add("BuildNumber", Application.buildGUID);
            PlayFabClientAPI.UpdatePlayerStatistics(
                new()
                {
                    AuthenticationContext = AuthenticationManager.PlayerAccount.AuthContext,
                    CustomTags = customTags,
                    Statistics = stats,
                },
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
    public delegate void LoadLeaderboardCallBack(List<LeaderboardEntryV2> entries);

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
            PlayFabClientAPI.GetLeaderboardAroundPlayer(
                new GetLeaderboardAroundPlayerRequest()
                {
                    AuthenticationContext = AuthenticationManager.PlayerAccount.AuthContext,
                    StatisticName = leaderboardName,
                    CustomTags = customTags,
                },
                response =>
                {
                    List<LeaderboardEntryV2> entries = new List<LeaderboardEntryV2>();
                    foreach (var entry in response.Leaderboard)
                    {
                        entries.Add(new LeaderboardEntryV2(entry.Profile.DisplayName, entry.StatValue, entry.Position));
                    }

                    callback(entries);

                    var dataAccessor = new DataAccessor(GetLeaderboardFileName(leaderboardName));
                    dataAccessor.Save(entries);

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
            var dataAccessor = new DataAccessor(GetLeaderboardFileName(leaderboardName));
            var cachedLeaderboard = dataAccessor.Load<List<LeaderboardEntryV2>>();
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
    
    /// <summary>
    /// Get Leaderboard By Mini Game and Ship Type 
    /// Fetch leaderboard data by name (aggregation of mini game and ship type name)
    /// Should take front end mini game and ship type data to here
    /// </summary>
    public void RequestLeaderboard(MiniGames miniGame, ShipTypes shipTypes)
    {
        PlayFabClientAPI.GetLeaderboard(new GetLeaderboardRequest
            {
                StatisticName = GetGameplayStatKey(miniGame, shipTypes),
                StartPosition = 0,
                MaxResultsCount = 10
            },
            (GetLeaderboardResult result) => HandleLeaderboardData(result),
            (error) =>
            {
                Debug.Log(error.GenerateErrorReport());
            });
    }

    /// <summary>
    /// Handle Leaderboard Data
    /// For now displaying leaderboard data in the console
    /// </summary>
    private void HandleLeaderboardData(GetLeaderboardResult result)
    {
        // The result doesn't return with leaderboard name, BLOCKBANDIT_ANY is a placeholder
        Debug.Log($"Leaderboard Manger - BLOCKBANDIT_ANY");
        var leaderboardEntryV2s = new List<LeaderboardEntryV2>();
        foreach (var entry in result.Leaderboard)
        {
            Debug.Log($"Leaderboard Manager - BLOCKBANDIT_ANY display name: {entry.DisplayName} score: {entry.StatValue.ToString()} position: {entry.Position.ToString()}");
            leaderboardEntryV2s.Add(new LeaderboardEntryV2(entry.DisplayName, entry.StatValue, entry.Position));
        }
        Debug.Log($"Leaderboard Manager - BLOCKBANDIT_ANY board version: {result.Version.ToString()}");
    }
    
    
}