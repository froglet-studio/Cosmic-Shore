using _Scripts._Core.Playfab_Models;
using PlayFab;
using PlayFab.ClientModels;
using StarWriter.Utility.Singleton;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeaderboardManager : SingletonPersistent<LeaderboardManager>
{
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

    readonly string OfflineStatsFileName = "offline_stats.data";
    readonly string CachedLeaderboardFileNamePrefix = "leaderboard_";

    bool online = false;

    void ComeOnline()
    {
        Debug.Log("LeaderboardManager - ComeOnline");
        online = true;
        ReportAndFlushOfflineStatistics();
    }

    void GoOffline()
    {
        Debug.Log("LeaderboardManager - GoOffline");
        online = false;
    }

    void OnEnable()
    {
        NetworkMonitor.NetworkConnectionFound += ComeOnline;
        NetworkMonitor.NetworkConnectionLost += GoOffline;
        AuthenticationManager.OnProfileLoaded += ReportAndFlushOfflineStatistics;
    }

    void OnDisable()
    {
        NetworkMonitor.NetworkConnectionFound -= ComeOnline;
        NetworkMonitor.NetworkConnectionLost -= GoOffline;
        AuthenticationManager.OnProfileLoaded -= ReportAndFlushOfflineStatistics;
    }

    void ReportAndFlushOfflineStatistics()
    {
        StartCoroutine(ReportAndFlushStatisticsCoroutine());
    }

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

    public void UpdateGameplayStatistic(MiniGames gameMode, ShipTypes shipType, int intensity, List<int> scores)
    {
        // Build list of statistics to update
        // One entry for each score for specific game mode/ship combination
        // One entry for each score for game mode any ship

        Debug.Log($"UpdateGamplayStats - gameMode:{gameMode}, shipType:{shipType}, intensity:{intensity}, score0:{scores[0]}");
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

    public string GetGameplayStatKey(MiniGames gameMode, ShipTypes shipType)
    {
        var statKey = gameMode.ToString().ToUpper() + "_" + shipType.ToString().ToUpper();

        Debug.Log("GetGameplayStatKey: " +  statKey);

        return statKey;
    }

    void UpdatePlayerStatistic(List<StatisticUpdate> stats)
    {
        UpdatePlayerStatistic(stats, new());
    }

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
            // TODO: custom tags lost
            var dataAccessor = new DataAccessor(OfflineStatsFileName);
            var offlineStatistics = dataAccessor.Load<List<StatisticUpdate>>();
            offlineStatistics.AddRange(stats);
            dataAccessor.Save(offlineStatistics);
        }
    }

    public delegate void LoadLeaderboardCallBack(List<LeaderboardEntryV2> entries);

    public void FetchLeaderboard(string leaderboardName, LoadLeaderboardCallBack callback)
    {
        FetchLeaderboard(leaderboardName, new(), callback);
    }

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

    string GetLeaderboardFileName(string leaderboardName)
    {
        return CachedLeaderboardFileNamePrefix + leaderboardName + ".data";
    }


    /*
    public void RequestLeaderboard()
    {
        PlayFabClientAPI.GetLeaderboard(new GetLeaderboardRequest
        {
            StatisticName = "HighScore",
            StartPosition = 0,
            MaxResultsCount = 10
        }, result => DisplayLeaderboard(result), FailureCallback);
    }
    */
}