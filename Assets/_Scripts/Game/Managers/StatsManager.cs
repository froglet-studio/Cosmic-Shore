using Newtonsoft.Json;
using CosmicShore.Core;
using CosmicShore.Utility.Singleton;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using System;
using CosmicShore.Game;

public class StatsManager : Singleton<StatsManager>
{
    public struct NodeStats
    {
            public int LifeFormsInNode;
    }

    [SerializeField] List<GameObject> EndOfRoundStatContainers;
    [SerializeField] public bool nodeGame = false;

    public Dictionary<Teams, RoundStats> TeamStats = new();
    public Dictionary<string, RoundStats> PlayerStats = new();
    
    public Dictionary<Teams, RoundStats> LastRoundTeamStats = new();
    public Dictionary<string, RoundStats> LastRoundPlayerStats = new();

    public Dictionary<string, NodeStats> CellStats = new();

    bool RecordStats = true;

    public void LifeformCreated(String nodeID)
    {
        if (!RecordStats)
            return;

        if (!CellStats.ContainsKey(nodeID))
        {
            CellStats.Add(nodeID, new NodeStats());
        }

        var nodeStats = CellStats[nodeID];
        nodeStats.LifeFormsInNode++;
        CellStats[nodeID] = nodeStats;
    }

    public void LifeformDestroyed(String nodeID)
    {
        if (!RecordStats)
            return;

        if (!CellStats.ContainsKey(nodeID))
        {
            CellStats.Add(nodeID, new NodeStats());
        }

        var nodeStats = CellStats[nodeID];
        nodeStats.LifeFormsInNode--;
        CellStats[nodeID] = nodeStats;
    }

    public void CrystalCollected(IShip ship, CrystalProperties crystalProperties)
    {
        if (!RecordStats)
            return;

        if (!EnsureDictionaryEntriesExist(ship.ShipStatus.Team, ship.ShipStatus.Player.PlayerName))
            return;

        
        TeamStats[ship.ShipStatus.Team].CrystalsCollected++;
        PlayerStats[ship.ShipStatus.Player.PlayerName].CrystalsCollected++;

        switch (crystalProperties.Element)
        {
            case Element.Omni:
                UpdateStatForTeamAndPlayer(ship.ShipStatus.Team, ship.ShipStatus.Player.PlayerName, stats => {
                    stats.OmniCrystalsCollected++; 
                    });
                break;

            case Element.Charge:
                UpdateStatForTeamAndPlayer(ship.ShipStatus.Team, ship.ShipStatus.Player.PlayerName, stats =>
                {
                    stats.ElementalCrystalsCollected++;
                    stats.ChargeCrystalValue += crystalProperties.crystalValue;
                });
                break;

            case Element.Mass:
                UpdateStatForTeamAndPlayer(ship.ShipStatus.Team, ship.ShipStatus.Player.PlayerName, stats =>
                {
                    stats.ElementalCrystalsCollected++;
                    stats.MassCrystalValue += crystalProperties.crystalValue;
                });
                break;

            case Element.Space:
                UpdateStatForTeamAndPlayer(ship.ShipStatus.Team, ship.ShipStatus.Player.PlayerName, stats =>
                {
                    stats.ElementalCrystalsCollected++;
                    stats.SpaceCrystalValue += crystalProperties.crystalValue;
                });
                break;

            case Element.Time:
                UpdateStatForTeamAndPlayer(ship.ShipStatus.Team, ship.ShipStatus.Player.PlayerName, stats =>
                {
                    stats.ElementalCrystalsCollected++;
                    stats.TimeCrystalValue += crystalProperties.crystalValue;
                });
                break;
        }
    }

    void UpdateStatForTeamAndPlayer(Teams team, string playerName, Action<RoundStats> updateAction)
    {
        if (TeamStats.ContainsKey(team))
            updateAction(TeamStats[team]);

        if (PlayerStats.ContainsKey(playerName))
            updateAction(PlayerStats[playerName]);
    }


    public void SkimmerShipCollision(IShip skimmingShip, IShip ship)
    {
        if (!RecordStats)
            return;

        if (!EnsureDictionaryEntriesExist(skimmingShip.ShipStatus.Team, skimmingShip.ShipStatus.Player.PlayerName))
            return;

        var roundStats = TeamStats[skimmingShip.ShipStatus.Team];
        roundStats.SkimmerShipCollisions++;
        TeamStats[skimmingShip.ShipStatus.Team] = roundStats;

        roundStats = PlayerStats[skimmingShip.ShipStatus.Player.PlayerName];
        roundStats.SkimmerShipCollisions++;
        PlayerStats[skimmingShip.ShipStatus.Player.PlayerName] = roundStats;
    }

    public void BlockCreated(Teams creatingTeam, string creatingPlayerName, TrailBlockProperties createdTrailBlockProperties)
    {
        if (!RecordStats)
            return;

        if (!EnsureDictionaryEntriesExist(creatingTeam, creatingPlayerName))
            return;

        var roundStats = TeamStats[creatingTeam];
        roundStats.BlocksCreated++;
        roundStats.BlocksRemaining++;
        roundStats.VolumeCreated += createdTrailBlockProperties.volume;
        roundStats.VolumeRemaining += createdTrailBlockProperties.volume;
        TeamStats[creatingTeam] = roundStats;

        roundStats = PlayerStats[creatingPlayerName];
        roundStats.BlocksCreated++;
        roundStats.BlocksRemaining++;
        roundStats.VolumeCreated += createdTrailBlockProperties.volume;
        roundStats.VolumeRemaining += createdTrailBlockProperties.volume;
        PlayerStats[creatingPlayerName] = roundStats;
    }

    public void BlockDestroyed(Teams destroyingTeam, string destroyingPlayerName, TrailBlockProperties destroyedTrailBlockProperties)
    {
        if (!RecordStats)
            return;

        if (!EnsureDictionaryEntriesExist(destroyingTeam, destroyingPlayerName))
            return;
        if (!EnsureDictionaryEntriesExist(destroyedTrailBlockProperties.trailBlock.Team, destroyedTrailBlockProperties.trailBlock.PlayerName))
            return;

        // Team Destruction Stats
        var roundStats = TeamStats[destroyingTeam];
        roundStats.BlocksDestroyed++;
        roundStats.FriendlyBlocksDestroyed += destroyingTeam == destroyedTrailBlockProperties.trailBlock.Team ? 1 : 0;
        roundStats.HostileBlocksDestroyed += destroyingTeam == destroyedTrailBlockProperties.trailBlock.Team ? 0 : 1;
        roundStats.VolumeDestroyed += destroyedTrailBlockProperties.volume;
        roundStats.FriendlyVolumeDestroyed += destroyingTeam == destroyedTrailBlockProperties.trailBlock.Team ? destroyedTrailBlockProperties.volume : 0;
        roundStats.HostileVolumeDestroyed += destroyingTeam == destroyedTrailBlockProperties.trailBlock.Team ? 0 : destroyedTrailBlockProperties.volume;
        TeamStats[destroyingTeam] = roundStats;

        // Team Remaining
        roundStats = TeamStats[destroyedTrailBlockProperties.trailBlock.Team];
        roundStats.BlocksRemaining--;
        roundStats.VolumeRemaining -= destroyedTrailBlockProperties.volume;
        TeamStats[destroyedTrailBlockProperties.trailBlock.Team] = roundStats;

        // Player Destruction Stats
        roundStats = PlayerStats[destroyingPlayerName];
        roundStats.BlocksDestroyed++;
        roundStats.FriendlyBlocksDestroyed += destroyingTeam == destroyedTrailBlockProperties.trailBlock.Team ? 1 : 0;
        roundStats.HostileBlocksDestroyed += destroyingTeam == destroyedTrailBlockProperties.trailBlock.Team ? 0 : 1;
        roundStats.VolumeDestroyed += destroyedTrailBlockProperties.volume;
        roundStats.FriendlyVolumeDestroyed += destroyingTeam == destroyedTrailBlockProperties.trailBlock.Team ? destroyedTrailBlockProperties.volume : 0;
        roundStats.HostileVolumeDestroyed += destroyingTeam == destroyedTrailBlockProperties.trailBlock.Team ? 0 : destroyedTrailBlockProperties.volume;
        PlayerStats[destroyingPlayerName] = roundStats;

        // Player Remaining
        roundStats = PlayerStats[destroyedTrailBlockProperties.trailBlock.PlayerName];
        roundStats.BlocksRemaining--;
        roundStats.VolumeRemaining -= destroyedTrailBlockProperties.volume;
        PlayerStats[destroyedTrailBlockProperties.trailBlock.PlayerName] = roundStats;
    }

    public void BlockRestored(Teams restoringTeam, string restoringPlayerName, TrailBlockProperties restoredTrailBlockProperties)
    {
        if (!RecordStats)
            return;

        if (!EnsureDictionaryEntriesExist(restoringTeam, restoringPlayerName))
            return;
        if (!EnsureDictionaryEntriesExist(restoredTrailBlockProperties.trailBlock.Team, restoredTrailBlockProperties.trailBlock.PlayerName))
            return;

        var roundStats = TeamStats[restoringTeam];
        roundStats.BlocksRestored++;
        roundStats.BlocksRemaining++;
        roundStats.VolumeRestored += restoredTrailBlockProperties.volume;
        roundStats.VolumeRemaining += restoredTrailBlockProperties.volume;
        TeamStats[restoringTeam] = roundStats;

        roundStats = PlayerStats[restoringPlayerName];
        roundStats.BlocksRestored++;
        roundStats.BlocksRemaining++;
        roundStats.VolumeRestored += restoredTrailBlockProperties.volume;
        roundStats.VolumeRemaining += restoredTrailBlockProperties.volume;
        PlayerStats[restoringPlayerName] = roundStats;
    }

    public void BlockVolumeModified(float volume, TrailBlockProperties modifiedTrailBlockProperties)
    {
        if (!RecordStats)
            return;

        if (!EnsureDictionaryEntriesExist(modifiedTrailBlockProperties.trailBlock.Team, modifiedTrailBlockProperties.trailBlock.PlayerName))
            return;

        // TODO: add Team modifying Stats separately for growth/shrink & friendly/hostyile

        // TODO: add Player modifying Stats separately for growth/shrink & friendly/hostyile

        // Team Remaining
        var roundStats = TeamStats[modifiedTrailBlockProperties.trailBlock.Team];
        roundStats.VolumeRemaining += volume;
        TeamStats[modifiedTrailBlockProperties.trailBlock.Team] = roundStats;

        // Player Remaining
        roundStats = PlayerStats[modifiedTrailBlockProperties.trailBlock.PlayerName];
        roundStats.VolumeRemaining += volume;
        PlayerStats[modifiedTrailBlockProperties.trailBlock.PlayerName] = roundStats;
    }

    public void BlockStolen(Teams stealingTeam, string stealingPlayerName, TrailBlockProperties stolenTrailBlockProperties)
    {
        if (!RecordStats)
            return;

        if (!EnsureDictionaryEntriesExist(stealingTeam, stealingPlayerName))
            return;
        if (!EnsureDictionaryEntriesExist(stolenTrailBlockProperties.trailBlock.Team, stolenTrailBlockProperties.trailBlock.PlayerName))
            return;

        // Team Stealing Stats
        var roundStats = TeamStats[stealingTeam];
        roundStats.BlocksStolen++;
        roundStats.BlocksRemaining++;
        roundStats.VolumeStolen += stolenTrailBlockProperties.volume;
        roundStats.VolumeRemaining += stolenTrailBlockProperties.volume;
        TeamStats[stealingTeam] = roundStats;

        // Player Stealing Stats
        roundStats = PlayerStats[stealingPlayerName];
        roundStats.BlocksStolen++;
        roundStats.BlocksRemaining++;
        roundStats.VolumeStolen += stolenTrailBlockProperties.volume;
        roundStats.VolumeRemaining += stolenTrailBlockProperties.volume;
        PlayerStats[stealingPlayerName] = roundStats;

        // Team Remaining
        roundStats = TeamStats[stolenTrailBlockProperties.trailBlock.Team];
        roundStats.BlocksRemaining--;
        roundStats.VolumeRemaining -= stolenTrailBlockProperties.volume;
        TeamStats[stolenTrailBlockProperties.trailBlock.Team] = roundStats;

        // Player Remaining
        roundStats = PlayerStats[stolenTrailBlockProperties.trailBlock.PlayerName];
        roundStats.BlocksRemaining--;
        roundStats.VolumeRemaining -= stolenTrailBlockProperties.volume;
        PlayerStats[stolenTrailBlockProperties.trailBlock.PlayerName] = roundStats;
    }

    public void AbilityActivated(Teams team, string playerName, InputEvents abilityType, float duration)
    {
        if (!RecordStats)
            return;

        if (!EnsureDictionaryEntriesExist(team, playerName))
            return;

        RoundStats roundStats;
        switch (abilityType)
        {
            case InputEvents.FullSpeedStraightAction:
                roundStats = TeamStats[team];
                roundStats.FullSpeedStraightAbilityActiveTime += duration;
                TeamStats[team] = roundStats;

                roundStats = PlayerStats[playerName];
                roundStats.FullSpeedStraightAbilityActiveTime += duration;
                PlayerStats[playerName] = roundStats;
                break;
            case InputEvents.RightStickAction:
                roundStats = TeamStats[team];
                roundStats.RightStickAbilityActiveTime += duration;
                TeamStats[team] = roundStats;

                roundStats = PlayerStats[playerName];
                roundStats.RightStickAbilityActiveTime += duration;
                PlayerStats[playerName] = roundStats;
                break;
            case InputEvents.LeftStickAction:
                roundStats = TeamStats[team];
                roundStats.LeftStickAbilityActiveTime += duration;
                TeamStats[team] = roundStats;

                roundStats = PlayerStats[playerName];
                roundStats.LeftStickAbilityActiveTime += duration;
                PlayerStats[playerName] = roundStats;
                break;
            case InputEvents.FlipAction:
                roundStats = TeamStats[team];
                roundStats.FlipAbilityActiveTime += duration;
                TeamStats[team] = roundStats;

                roundStats = PlayerStats[playerName];
                roundStats.FlipAbilityActiveTime += duration;
                PlayerStats[playerName] = roundStats;
                break;
            case InputEvents.Button1Action:
                roundStats = TeamStats[team];
                roundStats.Button1AbilityActiveTime += duration;
                TeamStats[team] = roundStats;

                roundStats = PlayerStats[playerName];
                roundStats.Button1AbilityActiveTime += duration;
                PlayerStats[playerName] = roundStats;
                break;
            case InputEvents.Button2Action:
                roundStats = TeamStats[team];
                roundStats.Button2AbilityActiveTime += duration;
                TeamStats[team] = roundStats;

                roundStats = PlayerStats[playerName];
                roundStats.Button2AbilityActiveTime += duration;
                PlayerStats[playerName] = roundStats;
                break;
            case InputEvents.Button3Action:
                roundStats = TeamStats[team];
                roundStats.Button3AbilityActiveTime += duration;
                TeamStats[team] = roundStats;

                roundStats = PlayerStats[playerName];
                roundStats.Button3AbilityActiveTime += duration;
                PlayerStats[playerName] = roundStats;
                break;
        }
    }

    void OnEnable()
    {
        GameManager.OnPlayGame += ResetStats;
        GameManager.OnGameOver += OutputRoundStats;
    }

    void OnDisable()
    {
        GameManager.OnPlayGame -= ResetStats;
        GameManager.OnGameOver -= OutputRoundStats;
    }

    /// <summary>
    /// Adds missing keys to the team and player stat dictionaries
    /// </summary>
    /// <param name="team">Team stat dictionary key</param>
    /// <param name="playerName">Player stat dictionary key</param>
    /// <returns>True if the dictionary contains the new keys after execution</returns>
    bool EnsureDictionaryEntriesExist(Teams team, string playerName)
    {
        try
        {
            TeamStats.TryAdd(team, new RoundStats());

            PlayerStats.TryAdd(playerName, new RoundStats());
        } catch (Exception e)
        {
            Debug.LogException(e);
        }

        return TeamStats.ContainsKey(team) && PlayerStats.ContainsKey(playerName);
    }

    public void ResetStats()
    {
        LastRoundTeamStats = TeamStats;
        LastRoundPlayerStats = PlayerStats;
        RecordStats = true;
        TeamStats.Clear();
        PlayerStats.Clear();
    }

    public void AddPlayer(Teams team, string playerName)
    {
        EnsureDictionaryEntriesExist(team, playerName);
    }

    public float GetTotalVolume()
    {
        return TeamStats.Values.Sum(stats => stats.VolumeRemaining);
    }

    public Vector4 GetTeamVolumes()
    {
        var greenVolume = TeamStats.TryGetValue(Teams.Jade, out var teamStat) ? teamStat.VolumeRemaining : 0;
        var redVolume = TeamStats.TryGetValue(Teams.Ruby, out teamStat) ? teamStat.VolumeRemaining : 0;
        var blueVolume = TeamStats.TryGetValue(Teams.Blue, out teamStat) ? teamStat.VolumeRemaining : 0;
        var yellowVolume = TeamStats.TryGetValue(Teams.Gold, out teamStat) ? teamStat.VolumeRemaining : 0;

        return new Vector4(greenVolume, redVolume, blueVolume, yellowVolume);
    }

    // TODO: P0 - we probably want a UI class that talks to the stats managar and updates the UI rather than doing it in here directly
    void OutputRoundStats()
    {
        RecordStats = false;

        string statsString = "<mspace=4.5px>\n";
        statsString += "<b>Field".PadRight(38) + " | ";

        foreach (var playerName in PlayerStats.Keys)
        {
            statsString += playerName.PadRight(10) + " | ";
        }
        statsString += "</b>\n";

        var fieldInfoList = typeof(RoundStats).GetFields();
        foreach (var fieldInfo in fieldInfoList)
        {
            string statValues = fieldInfo.Name.PadRight(35) + " | ";

            foreach (var playerName in PlayerStats.Keys)
            {
                object fieldValue = fieldInfo.GetValue(PlayerStats[playerName]);
                statValues += fieldValue.ToString().PadRight(10) + " | ";
            }

            statsString.Substring(statsString.Length - 3);
            statsString += statValues + "\n";
        }
        statsString += "</mspace=4.5px>";

        Debug.Log(statsString.ToString());
        Debug.Log(JsonConvert.SerializeObject(PlayerStats, Formatting.Indented));
        Debug.Log(JsonConvert.SerializeObject(TeamStats, Formatting.Indented));

        int i = 0;
        foreach (var team in TeamStats.Keys)
        {
            var container = EndOfRoundStatContainers[i];
            Debug.Log($"Team Stats - Team:{team}");
            i++;
        }

        i = 0;
        foreach (var player in PlayerStats.Keys)
        {
            Debug.Log($"PlayerStats - Player:{player}");

            var container = EndOfRoundStatContainers[i];
            container.transform.GetChild(0).GetComponent<TMP_Text>().text = player;
            container.transform.GetChild(1).GetComponent<TMP_Text>().text = PlayerStats[player].VolumeCreated.ToString("F0");
            container.transform.GetChild(2).GetComponent<TMP_Text>().text = PlayerStats[player].HostileVolumeDestroyed.ToString("F0");
            //Individual Impact or Score
            container.transform.GetChild(3).GetComponent<TMP_Text>().text = (PlayerStats[player].VolumeCreated + PlayerStats[player].HostileVolumeDestroyed
                                                                            - PlayerStats[player].FriendlyVolumeDestroyed + (2 * PlayerStats[player].VolumeStolen)).ToString("F0");

            i++;
        }

        //Calculate  and display winner
        var finalScore = TeamStats[Teams.Jade].VolumeRemaining - TeamStats[Teams.Ruby].VolumeRemaining;
        var winner = finalScore > 0 ? "Green" : "Red";
        Debug.Log($"Winner: {winner}");
    }
}