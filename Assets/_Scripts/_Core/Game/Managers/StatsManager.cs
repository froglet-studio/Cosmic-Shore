using System.Collections.Generic;
using UnityEngine;
using TMPro;
using StarWriter.Core;
using StarWriter.Utility.Singleton;
using Newtonsoft.Json;

// TODO: pull out into separate file
[System.Serializable]
public struct RoundStats
{
    public int blocksCreated;
    public int blocksDestroyed;
    public int blocksRestored;
    public int blocksStolen;
    public int blocksRemaining;
    public int friendlyBlocksDestroyed;
    public int hostileBlocksDestroyed;
    public float volumeCreated;
    public float volumeDestroyed;
    public float volumeRestored;
    public float volumeStolen;
    public float volumeRemaining;
    public float friendlyVolumeDestroyed;
    public float hostileVolumeDestroyed;
    public int crystalsCollected;
    public float fullSpeedStraightAbilityActiveTime;
    public float rightStickAbilityActiveTime;
    public float leftStickAbilityActiveTime;
    public float flipAbilityActiveTime;

    public RoundStats(bool dummy = false)
    {
        blocksCreated = 0;
        blocksDestroyed = 0;
        blocksRestored = 0;
        blocksStolen = 0;
        blocksRemaining = 0;
        friendlyBlocksDestroyed = 0;
        hostileBlocksDestroyed = 0;
        volumeCreated = 0;
        volumeDestroyed = 0;
        volumeRestored = 0;
        volumeStolen = 0;
        volumeRemaining = 0;
        friendlyVolumeDestroyed = 0;
        hostileVolumeDestroyed = 0;
        crystalsCollected = 0;
        fullSpeedStraightAbilityActiveTime = 0;
        rightStickAbilityActiveTime = 0;
        leftStickAbilityActiveTime = 0;
        flipAbilityActiveTime = 0;
    }
}

public class StatsManager : Singleton<StatsManager>
{
    [SerializeField] List<GameObject> EndOfRoundStatContainers;
    [SerializeField] public bool nodeGame = false;

    Dictionary<Teams, RoundStats> teamStats = new Dictionary<Teams, RoundStats>();
    Dictionary<string, RoundStats> playerStats = new Dictionary<string, RoundStats>();

    public void CrystalCollected(Ship ship, CrystalProperties crystalProperties)
    {
        MaybeCreateDictionaryEntries(ship.Team, ship.Player.PlayerName);

        var roundStats = teamStats[ship.Team];
        roundStats.crystalsCollected++;
        teamStats[ship.Team] = roundStats;

        roundStats = playerStats[ship.Player.PlayerName];
        roundStats.crystalsCollected++;
        playerStats[ship.Player.PlayerName] = roundStats;
    }

    public void BlockCreated(Teams creatingTeam, string creatingPlayerName, TrailBlockProperties createdTrailBlockProperties)
    {
        MaybeCreateDictionaryEntries(creatingTeam, creatingPlayerName);

        var roundStats = teamStats[creatingTeam];
        roundStats.blocksCreated++;
        roundStats.blocksRemaining++;
        roundStats.volumeCreated += createdTrailBlockProperties.volume;
        roundStats.volumeRemaining += createdTrailBlockProperties.volume;
        teamStats[creatingTeam] = roundStats;

        roundStats = playerStats[creatingPlayerName];
        roundStats.blocksCreated++;
        roundStats.blocksRemaining++;
        roundStats.volumeCreated += createdTrailBlockProperties.volume;
        roundStats.volumeRemaining += createdTrailBlockProperties.volume;
        playerStats[creatingPlayerName] = roundStats;
    }

    public void BlockDestroyed(Teams destroyingTeam, string destroyingPlayerName, TrailBlockProperties destroyedTrailBlockProperties)
    {
        MaybeCreateDictionaryEntries(destroyingTeam, destroyingPlayerName);

        // Team Destruction Stats
        var roundStats = teamStats[destroyingTeam];
        roundStats.blocksDestroyed++;
        roundStats.friendlyBlocksDestroyed += destroyingTeam == destroyedTrailBlockProperties.trail.Team ? 1 : 0;
        roundStats.hostileBlocksDestroyed += destroyingTeam == destroyedTrailBlockProperties.trail.Team ? 0 : 1;
        roundStats.volumeDestroyed += destroyedTrailBlockProperties.volume;
        roundStats.friendlyVolumeDestroyed += destroyingTeam == destroyedTrailBlockProperties.trail.Team ? destroyedTrailBlockProperties.volume : 0;
        roundStats.hostileVolumeDestroyed += destroyingTeam == destroyedTrailBlockProperties.trail.Team ? 0 : destroyedTrailBlockProperties.volume;
        teamStats[destroyingTeam] = roundStats;

        // Team Remaining
        roundStats = teamStats[destroyedTrailBlockProperties.trail.Team];
        roundStats.blocksRemaining--;
        roundStats.volumeRemaining-= destroyedTrailBlockProperties.volume;
        teamStats[destroyedTrailBlockProperties.trail.Team] = roundStats;

        // Player Destruction Stats
        roundStats = playerStats[destroyingPlayerName];
        roundStats.blocksDestroyed++;
        roundStats.friendlyBlocksDestroyed += destroyingTeam == destroyedTrailBlockProperties.trail.Team ? 1 : 0;
        roundStats.hostileBlocksDestroyed += destroyingTeam == destroyedTrailBlockProperties.trail.Team ? 0 : 1;
        roundStats.volumeDestroyed += destroyedTrailBlockProperties.volume;
        roundStats.friendlyVolumeDestroyed += destroyingTeam == destroyedTrailBlockProperties.trail.Team ? destroyedTrailBlockProperties.volume : 0;
        roundStats.hostileVolumeDestroyed += destroyingTeam == destroyedTrailBlockProperties.trail.Team ? 0 : destroyedTrailBlockProperties.volume;
        playerStats[destroyingPlayerName] = roundStats;

        // Player Remaining
        roundStats = playerStats[destroyedTrailBlockProperties.trail.PlayerName];
        roundStats.blocksRemaining--;
        roundStats.volumeRemaining -= destroyedTrailBlockProperties.volume;
        playerStats[destroyedTrailBlockProperties.trail.PlayerName] = roundStats;
    }

    public void BlockRestored(Teams restoringTeam, string restoringPlayerName, TrailBlockProperties restoredTrailBlockProperties) {
        MaybeCreateDictionaryEntries(restoringTeam, restoringPlayerName);

        var roundStats = teamStats[restoringTeam];
        roundStats.blocksRemaining++;
        roundStats.volumeRemaining += restoredTrailBlockProperties.volume;
        teamStats[restoringTeam] = roundStats;

        roundStats = playerStats[restoringPlayerName];
        roundStats.blocksRemaining++;
        roundStats.volumeRemaining += restoredTrailBlockProperties.volume;
        playerStats[restoringPlayerName] = roundStats;
    }

    public void BlockStolen(Teams stealingTeam, string stealingPlayerName, TrailBlockProperties stolenTrailBlockProperties) {
        MaybeCreateDictionaryEntries(stealingTeam, stealingPlayerName);

        // Team Stealing Stats
        var roundStats = teamStats[stealingTeam];
        roundStats.blocksStolen++;
        roundStats.blocksRemaining++;
        roundStats.volumeStolen += stolenTrailBlockProperties.volume;
        roundStats.volumeRemaining += stolenTrailBlockProperties.volume;
        teamStats[stealingTeam] = roundStats;

        // Player Stealing Stats
        roundStats = playerStats[stealingPlayerName];
        roundStats.blocksStolen++;
        roundStats.blocksRemaining++;
        roundStats.volumeStolen += stolenTrailBlockProperties.volume;
        roundStats.volumeRemaining += stolenTrailBlockProperties.volume;
        playerStats[stealingPlayerName] = roundStats;

        // Team Remaining
        roundStats = teamStats[stolenTrailBlockProperties.trail.Team];
        roundStats.blocksRemaining--;
        roundStats.volumeRemaining -= stolenTrailBlockProperties.volume;
        teamStats[stolenTrailBlockProperties.trail.Team] = roundStats;

        // Player Remaining
        roundStats = playerStats[stolenTrailBlockProperties.trail.PlayerName];
        roundStats.blocksRemaining--;
        roundStats.volumeRemaining -= stolenTrailBlockProperties.volume;
        playerStats[stolenTrailBlockProperties.trail.PlayerName] = roundStats;
    }

    public void AbilityActivated(Teams team, string playerName, ShipActiveAbilityTypes abilityType, float duration)
    {
        MaybeCreateDictionaryEntries(team, playerName);
        RoundStats roundStats;
        switch (abilityType)
        {
            case ShipActiveAbilityTypes.FullSpeedStraightAbility:
                roundStats = teamStats[team];
                roundStats.fullSpeedStraightAbilityActiveTime += duration;
                teamStats[team] = roundStats;

                roundStats = playerStats[playerName];
                roundStats.fullSpeedStraightAbilityActiveTime += duration;
                playerStats[playerName] = roundStats;
                break;
            case ShipActiveAbilityTypes.RightStickAbility:
                roundStats = teamStats[team];
                roundStats.rightStickAbilityActiveTime += duration;
                teamStats[team] = roundStats;

                roundStats = playerStats[playerName];
                roundStats.rightStickAbilityActiveTime += duration;
                playerStats[playerName] = roundStats;
                break;
            case ShipActiveAbilityTypes.LeftStickAbility:
                roundStats = teamStats[team];
                roundStats.leftStickAbilityActiveTime += duration;
                teamStats[team] = roundStats;

                roundStats = playerStats[playerName];
                roundStats.leftStickAbilityActiveTime += duration;
                playerStats[playerName] = roundStats;
                break;
            case ShipActiveAbilityTypes.FlipAbility:
                roundStats = teamStats[team];
                roundStats.flipAbilityActiveTime += duration;
                teamStats[team] = roundStats;

                roundStats = playerStats[playerName];
                roundStats.flipAbilityActiveTime += duration;
                playerStats[playerName] = roundStats;
                break;
        }
    }

    void OnEnable()
    {
        GameManager.onPlayGame += ResetStats;
        GameManager.onGameOver += OutputRoundStats;
    }

    void OnDisable()
    {
        GameManager.onPlayGame -= ResetStats;
        GameManager.onGameOver -= OutputRoundStats;
    }

    void MaybeCreateDictionaryEntries(Teams team, string playerName)
    {
        if (!teamStats.ContainsKey(team))
            teamStats.Add(team, new RoundStats());

        if (!playerStats.ContainsKey(playerName))
            playerStats.Add(playerName, new RoundStats());
    }

    void ResetStats()
    {
        teamStats = new Dictionary<Teams, RoundStats>();
        playerStats = new Dictionary<string, RoundStats>();
    }

    // TODO: we probably want a UI class that talks to the stats managaer and updates the UI rather than doing it in here directly
    void OutputRoundStats()
    {

        foreach (var team in teamStats.Keys)
        {
            Debug.LogWarning($"Team Stats - Team:{team}");
            Debug.LogWarning(JsonConvert.SerializeObject(teamStats, Formatting.Indented));
            //Debug.LogWarning($"Team Stats - Team:{team}, Crystals Collected: {teamStats[team].crystalsCollected} ");
            //Debug.LogWarning($"Team Stats - Team:{team}, Blocks Created: {teamStats[team].blocksCreated} ");
            //Debug.LogWarning($"Team Stats - Team:{team}, Blocks Destroyed: {teamStats[team].friendlyBlocksDestroyed} ");
            //Debug.LogWarning($"Team Stats - Team:{team}, Volume Created: {teamStats[team].volumeCreated} ");
            //Debug.LogWarning($"Team Stats - Team:{team}, Volume Destroyed: {teamStats[team].friendlyVolumeDestroyed} ");
        }

        int i = 0;
        foreach (var player in playerStats.Keys)
        {
            Debug.LogWarning($"PlayerStats - Player:{player}");
            Debug.LogWarning(JsonConvert.SerializeObject(playerStats, Formatting.Indented));

            //Debug.LogWarning($"PlayerStats - Player:{player}, Crystals Collected: {playerStats[player].crystalsCollected} ");
            //Debug.LogWarning($"PlayerStats - Player:{Player}, Blocks Created: {playerStats[Player].blocksCreated} ");
            //Debug.LogWarning($"PlayerStats - Player:{Player}, Blocks Destroyed: {playerStats[Player].friendlyBlocksDestroyed} ");
            //Debug.LogWarning($"PlayerStats - Player:{player}, Volume Created: {playerStats[player].volumeCreated} ");
            //Debug.LogWarning($"PlayerStats - Player:{player}, Volume Destroyed: {playerStats[player].friendlyVolumeDestroyed} ");

            var container = EndOfRoundStatContainers[i];
            container.transform.GetChild(0).GetComponent<TMP_Text>().text = player;
            container.transform.GetChild(1).GetComponent<TMP_Text>().text = playerStats[player].volumeCreated.ToString("F1");
            container.transform.GetChild(2).GetComponent<TMP_Text>().text = playerStats[player].friendlyVolumeDestroyed.ToString("F1");
            container.transform.GetChild(3).GetComponent<TMP_Text>().text = playerStats[player].crystalsCollected.ToString("D");

            i++;
        }
    }
}