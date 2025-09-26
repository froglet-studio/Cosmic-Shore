using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using CosmicShore.Game;
using CosmicShore.SOAP;
using CosmicShore.Utilities;
using Obvious.Soap;


namespace CosmicShore.Core
{
    [System.Serializable]
    public struct CellStats
    {
        public int LifeFormsInCell;
    }

    [Serializable]
    public struct CrystalStats
    {
        public string PlayerName;
        public Element Element;
        public float Value;
    }

    [Serializable]
    public struct PrismStats
    {
        public string PlayerName;
        public float Volume;
        public string OtherPlayerName;
    }

    [Serializable]
    public struct AbilityStats
    {
        public string PlayerName;
        public InputEvents ControlType;
        public float Duration;
    }

    public class StatsManager : Singleton<StatsManager>
    {
        [SerializeField]
        List<GameObject> EndOfRoundStatContainers;
        
        [SerializeField]
        MiniGameDataSO _miniGameData;
        
        // Stats dictionaries
        /*public Dictionary<Teams, IRoundStats> TeamStats = new();
        public Dictionary<string, IRoundStats> PlayerStats = new();

        public Dictionary<Teams, IRoundStats> LastRoundTeamStats = new();
        public Dictionary<string, IRoundStats> LastRoundPlayerStats = new();*/

        // public Dictionary<string, NodeStats> CellStats = new();

        protected bool allowRecord = true;

        protected virtual void OnEnable()
        {
            // TODO: Need to find out from where to raise events in order to reset stats or output round stats.
            /*_onPlayGame.OnRaised += ResetStats;
            _onGameOver.OnRaised += OutputRoundStats;*/
        }

        protected virtual void OnDisable()
        {
            /*_onPlayGame.OnRaised -= ResetStats;
            _onGameOver.OnRaised -= OutputRoundStats;*/
        }
        
        public virtual IRoundStats GetOrCreateRoundStats(Domains domain) => new RoundStats();
        
        public void LifeformCreated(int cellID)
        {
            if (!allowRecord) return;

            var cellStatsList = _miniGameData.CellStatsList;
            
            if (!cellStatsList.ContainsKey(cellID))
                cellStatsList[cellID] = new CellStats();

            var cs = cellStatsList[cellID];
            cs.LifeFormsInCell++;
            cellStatsList[cellID] = cs;
        }

        public void LifeformDestroyed(int cellID)
        {
            if (!allowRecord) return;

            var cellStatsList = _miniGameData.CellStatsList;
            
            if (!cellStatsList.ContainsKey(cellID))
                cellStatsList[cellID] = new CellStats();

            var cs = cellStatsList[cellID];
            cs.LifeFormsInCell--;
            cellStatsList[cellID] = cs;
        }
        
        public void CrystalCollected(CrystalStats crystalStats)
        {
            if (!allowRecord) return;

            // var team = vessel.VesselStatus.Team;
            var playerName = crystalStats.PlayerName;
            /*if (!EnsureDictionaryEntriesExist(team, playerName)) return;

            TeamStats[team].CrystalsCollected++;
            PlayerStats[playerName].CrystalsCollected++;*/

            if (!_miniGameData.TryGetRoundStats(playerName, out IRoundStats stats))
                return;
            stats.CrystalsCollected++;
            
            switch (crystalStats.Element)
            {
                case Element.Omni:
                    UpdateStatForPlayer(playerName, s =>
                    {
                        s.OmniCrystalsCollected++;
                    });
                    break;

                case Element.Charge:
                    UpdateStatForPlayer(playerName, s =>
                    {
                        s.ElementalCrystalsCollected++;
                        s.ChargeCrystalValue += crystalStats.Value;
                    });
                    break;

                case Element.Mass:
                    UpdateStatForPlayer(playerName, s =>
                    {
                        s.ElementalCrystalsCollected++;
                        s.MassCrystalValue += crystalStats.Value;
                    });
                    break;

                case Element.Space:
                    UpdateStatForPlayer(playerName, s =>
                    {
                        s.ElementalCrystalsCollected++;
                        s.SpaceCrystalValue += crystalStats.Value;
                    });
                    break;

                case Element.Time:
                    UpdateStatForPlayer(playerName, s =>
                    {
                        s.ElementalCrystalsCollected++;
                        s.TimeCrystalValue += crystalStats.Value;
                    });
                    break;
                case Element.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // public void ExecuteSkimmerShipCollision(IVessel skimmingShip, IVessel vessel)
        public void ExecuteSkimmerShipCollision(string skimmerPlayerName)
        {
            if (!allowRecord) return;

            // var team = skimmingShip.VesselStatus.Team;

            if (!_miniGameData.TryGetRoundStats(skimmerPlayerName, out var roundStats))
                return;
            roundStats.SkimmerShipCollisions++;
            
            /*if (!EnsureDictionaryEntriesExist(team, playerName)) return;

            TeamStats[team].SkimmerShipCollisions++;
            PlayerStats[playerName].SkimmerShipCollisions++;*/
        }

        public void PrismCreated(PrismStats prismStats)
        {
            if (!allowRecord) return;

            if (!_miniGameData.TryGetRoundStats(prismStats.PlayerName, out var roundStats))
                return;

            roundStats.BlocksCreated++;
            roundStats.PrismsRemaining++;
            roundStats.VolumeCreated += prismStats.Volume;
            roundStats.VolumeRemaining += prismStats.Volume;
            
            /*if (!EnsureDictionaryEntriesExist(creatingTeam, creatingPlayerName)) return;

            TeamStats[creatingTeam].BlocksCreated++;
            TeamStats[creatingTeam].BlocksRemaining++;
            TeamStats[creatingTeam].VolumeCreated += createdTrailBlockProperties.volume;
            TeamStats[creatingTeam].VolumeRemaining += createdTrailBlockProperties.volume;

            var pStats = PlayerStats[creatingPlayerName];
            pStats.BlocksCreated++;
            pStats.BlocksRemaining++;
            pStats.VolumeCreated += createdTrailBlockProperties.volume;
            pStats.VolumeRemaining += createdTrailBlockProperties.volume;*/
        }

        public void PrismDestroyed(PrismStats prismStats)
        {
            if (!allowRecord) return;

            // var victimTeam = destroyedTrailBlockProperties.trailBlock.Team;
            var victimPlayerName = prismStats.OtherPlayerName;
            var attackingPlayerName = prismStats.PlayerName;

            // Ensure both attacker and victim have stats entries
            /*if (!EnsureDictionaryEntriesExist(destroyingTeam, destroyingPlayerName)) return;
            if (!EnsureDictionaryEntriesExist(victimTeam, victimPlayerName)) return;*/

            // Attacker team stats
            if (!_miniGameData.TryGetRoundStats(attackingPlayerName, out IRoundStats attackerPlayerStats))
                return; // TeamStats[destroyingTeam];
            
            if (!_miniGameData.TryGetRoundStats(victimPlayerName, out IRoundStats victimPlayerStats))
                return;// TeamStats[victimTeam];
            
            attackerPlayerStats.BlocksDestroyed++;
            if (attackingPlayerName == victimPlayerName)
                attackerPlayerStats.FriendlyPrismsDestroyed++;
            else
                attackerPlayerStats.HostilePrismsDestroyed++;
            attackerPlayerStats.VolumeDestroyed += prismStats.Volume;
            if (attackingPlayerName == victimPlayerName)
                attackerPlayerStats.FriendlyVolumeDestroyed += prismStats.Volume;
            else
                attackerPlayerStats.HostileVolumeDestroyed += prismStats.Volume;

            // Victim team remaining stats
            victimPlayerStats.PrismsRemaining--;
            victimPlayerStats.VolumeRemaining -= prismStats.Volume;

            // Attacker player stats
            attackerPlayerStats.BlocksDestroyed++;
            if (attackingPlayerName == victimPlayerName)
                attackerPlayerStats.FriendlyPrismsDestroyed++;
            else
                attackerPlayerStats.HostilePrismsDestroyed++;
            
            attackerPlayerStats.VolumeDestroyed += prismStats.Volume;
            if (attackingPlayerName == victimPlayerName)
                attackerPlayerStats.FriendlyVolumeDestroyed += prismStats.Volume;
            else
                attackerPlayerStats.HostileVolumeDestroyed += prismStats.Volume;

            // Victim player remaining stats
            /*victimTeamStats.BlocksRemaining--;
            victimTeamStats.VolumeRemaining -= destroyedTrailBlockProperties.volume;*/
        }

        public void PrismRestored(PrismStats prismStats)
        {
            if (!allowRecord) return;

            var restoringPlayerName = prismStats.PlayerName;

            if (!_miniGameData.TryGetRoundStats(restoringPlayerName, out IRoundStats roundStats))
                return;
            
            /*var ownerPlayerName = restoredTrailBlockProperties.trailBlock.PlayerName;

            if (!EnsureDictionaryEntriesExist(restoringTeam, restoringPlayerName)) return;
            if (!EnsureDictionaryEntriesExist(ownerTeam, ownerPlayerName)) return;*/

            // var teamStats = TeamStats[restoringTeam];
            roundStats.BlocksRestored++;
            roundStats.PrismsRemaining++;
            roundStats.VolumeRestored += prismStats.Volume;
            roundStats.VolumeRemaining += prismStats.Volume;

            // var playerStats = PlayerStats[restoringPlayerName];
            /*roundStats.BlocksRestored++;
            roundStats.BlocksRemaining++;
            roundStats.VolumeRestored += restoredTrailBlockProperties.volume;
            roundStats.VolumeRemaining += restoredTrailBlockProperties.volume;*/
        }

        public void PrismVolumeModified(PrismStats prismStats)
        {
            if (!allowRecord) return;

            // var ownerTeam = modifiedTrailBlockProperties.trailBlock.Team;
            var ownerPlayerName = prismStats.OtherPlayerName;
            
            if (!_miniGameData.TryGetRoundStats(ownerPlayerName, out IRoundStats roundStats))
                return;

            // if (!EnsureDictionaryEntriesExist(ownerTeam, ownerPlayerName)) return;

            roundStats.VolumeCreated += prismStats.Volume;
            roundStats.VolumeRemaining += prismStats.Volume;
        }

        // public void PrismStolen(Teams stealingTeam, string stealingPlayerName, PrismProperties stolenTrailBlockProperties)
        public void PrismStolen(PrismStats prismStats)
        {
            if (!allowRecord) return;

            // var victimTeam = stolenTrailBlockProperties.trailBlock.Team;

            /*if (!EnsureDictionaryEntriesExist(stealingTeam, stealingPlayerName)) return;
            if (!EnsureDictionaryEntriesExist(victimTeam, victimPlayerName)) return;

            // Stealing team stats
            var stealingTeamStats = TeamStats[stealingTeam];*/

            var stealingPlayerName = prismStats.PlayerName;
            if (!_miniGameData.TryGetRoundStats(stealingPlayerName, out IRoundStats stealingPlayerStats))
                return;
            
            stealingPlayerStats.PrismStolen++;
            stealingPlayerStats.PrismsRemaining++;
            stealingPlayerStats.VolumeStolen += prismStats.Volume;
            stealingPlayerStats.VolumeRemaining += prismStats.Volume;

            // Stealing player stats
            // var stealingPlayerStats = PlayerStats[stealingPlayerName];
            
            /*stealingPlayerStats.BlocksStolen++;
            stealingPlayerStats.BlocksRemaining++;
            stealingPlayerStats.VolumeStolen += stolenTrailBlockProperties.volume;
            stealingPlayerStats.VolumeRemaining += stolenTrailBlockProperties.volume;*/

            var victimPlayerName = prismStats.OtherPlayerName;
            if (!_miniGameData.TryGetRoundStats(victimPlayerName, out IRoundStats victimPlayerStats))
                return;
            
            // Victim team remaining
            // var victimTeamStats = TeamStats[victimTeam];
            victimPlayerStats.PrismsRemaining--;
            victimPlayerStats.VolumeRemaining -= prismStats.Volume;

            // Victim player remaining
            // var victimPlayerStats = PlayerStats[victimPlayerName];
            /*victimPlayerStats.BlocksRemaining--;
            victimPlayerStats.VolumeRemaining -= stolenTrailBlockProperties.volume;*/
        }

        // public void AbilityActivated(Teams team, string playerName, InputEvents abilityType, float duration)
        public void RegisterAbilityExecuted(AbilityStats abilityStats)
        {
            if (!allowRecord) return;
            /*if (!EnsureDictionaryEntriesExist(team, playerName)) return;

            var teamStats = TeamStats[team];
            var playerStats = PlayerStats[playerName];*/
            
            if (!_miniGameData.TryGetRoundStats(abilityStats.PlayerName, out IRoundStats playerStats))
                return;

            switch (abilityStats.ControlType)
            {
                case InputEvents.FullSpeedStraightAction:
                    // teamStats.FullSpeedStraightAbilityActiveTime += duration;
                    playerStats.FullSpeedStraightAbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.RightStickAction:
                    // teamStats.RightStickAbilityActiveTime += duration;
                    playerStats.RightStickAbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.LeftStickAction:
                    // teamStats.LeftStickAbilityActiveTime += duration;
                    playerStats.LeftStickAbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.FlipAction:
                    // teamStats.FlipAbilityActiveTime += duration;
                    playerStats.FlipAbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.Button1Action:
                    // teamStats.Button1AbilityActiveTime += duration;
                    playerStats.Button1AbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.Button2Action:
                    // teamStats.Button2AbilityActiveTime += duration;
                    playerStats.Button2AbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.Button3Action:
                    // teamStats.Button3AbilityActiveTime += duration;
                    playerStats.Button3AbilityActiveTime += abilityStats.Duration;
                    break;
            }
        }
        
        /*
         
         /// <summary>
        /// Ensures the given team and player have entries in their dictionaries.
        /// </summary>
        bool EnsureDictionaryEntriesExist(Teams team, string playerName)
        {
            if (!TeamStats.ContainsKey(team))
                TeamStats[team] = GetRoundStats(team);
            if (!PlayerStats.ContainsKey(playerName))
                PlayerStats[playerName] = GetRoundStats(team);
            return true;
        }
         
        public void ResetStats()
        {
            LastRoundTeamStats = new Dictionary<Teams, IRoundStats>(TeamStats);
            LastRoundPlayerStats = new Dictionary<string, IRoundStats>(PlayerStats);
            RecordStats = true;
            TeamStats.Clear();
            PlayerStats.Clear();
            CellStats.Clear();
        }

        public void AddPlayer(Teams team, string playerName)
        {
            EnsureDictionaryEntriesExist(team, playerName);
        }
        */

        
        
        // void UpdateStatForTeamAndPlayer(Teams team, string playerName, Action<IRoundStats> updateAction)
        void UpdateStatForPlayer(string playerName, Action<IRoundStats> updateAction)
        {
            if (!_miniGameData.TryGetRoundStats(playerName, out var roundStats))
                return;

            updateAction(roundStats);
            
            // TODO - Remove following code after confirmation.
            /*if (TeamStats.TryGetValue(team, out var teamStats))
                updateAction(teamStats);

            if (PlayerStats.TryGetValue(playerName, out var playerStats))
                updateAction(playerStats);*/
        }

        /*protected void OutputRoundStats()
        {
            allowRecord = false;

            // Build a text table using properties of IRoundStats
            var sb = new StringBuilder();
            sb.AppendLine("<mspace=4.5px>");
            sb.Append("<b>Field".PadRight(38) + " | ");
            foreach (var playerName in PlayerStats.Keys)
            {
                sb.Append(playerName.PadRight(10) + " | ");
            }
            sb.AppendLine("</b>");

            var propList = typeof(IRoundStats).GetProperties();
            foreach (var propInfo in propList)
            {
                sb.Append(propInfo.Name.PadRight(35) + " | ");
                foreach (var playerName in PlayerStats.Keys)
                {
                    var val = propInfo.GetValue(PlayerStats[playerName]);
                    sb.Append(val.ToString().PadRight(10) + " | ");
                }
                sb.AppendLine();
            }
            sb.AppendLine("</mspace=4.5px>");

            Debug.Log(sb.ToString());

            // Serialize dictionaries
            Debug.Log(JsonConvert.SerializeObject(PlayerStats, Formatting.Indented));
            Debug.Log(JsonConvert.SerializeObject(TeamStats, Formatting.Indented));

            // Populate end-of-round UI (still tightly coupled�consider moving to a UI controller)
            int i = 0;
            foreach (var playerName in PlayerStats.Keys)
            {
                var stats = PlayerStats[playerName];
                var container = EndOfRoundStatContainers[i];
                container.transform.GetChild(0).GetComponent<TMP_Text>().text = playerName;
                container.transform.GetChild(1).GetComponent<TMP_Text>().text = stats.VolumeCreated.ToString("F0");
                container.transform.GetChild(2).GetComponent<TMP_Text>().text = stats.HostileVolumeDestroyed.ToString("F0");
                float score = stats.VolumeCreated
                            + stats.HostileVolumeDestroyed
                            - stats.FriendlyVolumeDestroyed
                            + (2f * stats.VolumeStolen);
                container.transform.GetChild(3).GetComponent<TMP_Text>().text = score.ToString("F0");
                i++;
            }

            // Determine winner
            float greenRem = TeamStats.TryGetValue(Teams.Jade, out var gTeamStats) ? gTeamStats.VolumeRemaining : 0f;
            float redRem = TeamStats.TryGetValue(Teams.Ruby, out var rTeamStats) ? rTeamStats.VolumeRemaining : 0f;
            string winner = (greenRem - redRem) > 0f ? "Green" : "Red";
            Debug.Log($"Winner: {winner}");
        }*/
    }
}
