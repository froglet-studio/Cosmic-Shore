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


namespace CosmicShore.Core
{
    [System.Serializable]
    public struct NodeStats
    {
        public int LifeFormsInNode;
    }
    
    public class StatsManager : Singleton<StatsManager>
    {
        [SerializeField]
        List<GameObject> EndOfRoundStatContainers;
        
        [SerializeField]
        MiniGameDataSO _miniGameData;
        
        [Header("Event Channels")]
        [SerializeField]
        protected ScriptableEventTrailBlockEventData _onTrailBlockCreatedEventChannel;
        [SerializeField]
        protected ScriptableEventTrailBlockEventData _onTrailBlockDestroyedEventChannel;
        [SerializeField]
        protected ScriptableEventTrailBlockEventData _onTrailBlockRestoredEventChannel;
        
        
        // Stats dictionaries
        /*public Dictionary<Teams, IRoundStats> TeamStats = new();
        public Dictionary<string, IRoundStats> PlayerStats = new();

        public Dictionary<Teams, IRoundStats> LastRoundTeamStats = new();
        public Dictionary<string, IRoundStats> LastRoundPlayerStats = new();*/

        public Dictionary<string, NodeStats> CellStats = new();

        bool allowRecord = true;

        protected virtual void OnEnable()
        {
            // TODO: Need to find out from where to raise events in order to reset stats or output round stats.
            /*_onPlayGame.OnRaised += ResetStats;
            _onGameOver.OnRaised += OutputRoundStats;*/
            
            _onTrailBlockCreatedEventChannel.OnRaised += OnBlockCreated;
            _onTrailBlockDestroyedEventChannel.OnRaised += OnBlockDestroyed;
            _onTrailBlockRestoredEventChannel.OnRaised += OnBlockRestored;
        }

        protected virtual void OnDisable()
        {
            /*_onPlayGame.OnRaised -= ResetStats;
            _onGameOver.OnRaised -= OutputRoundStats;*/
            
            _onTrailBlockCreatedEventChannel.OnRaised -= OnBlockCreated;
            _onTrailBlockDestroyedEventChannel.OnRaised -= OnBlockDestroyed;
            _onTrailBlockRestoredEventChannel.OnRaised -= OnBlockRestored;
        }
        
        public virtual IRoundStats GetOrCreateRoundStats(Teams team) => new RoundStats();
        
        public void LifeformCreated(string cellID)
        {
            if (!allowRecord) return;

            if (!CellStats.ContainsKey(cellID))
                CellStats[cellID] = new NodeStats();

            var ns = CellStats[cellID];
            ns.LifeFormsInNode++;
            CellStats[cellID] = ns;
        }

        public void LifeformDestroyed(string nodeID)
        {
            if (!allowRecord) return;

            if (!CellStats.ContainsKey(nodeID))
                CellStats[nodeID] = new NodeStats();

            var ns = CellStats[nodeID];
            ns.LifeFormsInNode--;
            CellStats[nodeID] = ns;
        }
        
        public void CrystalCollected(IShip ship, CrystalProperties crystalProperties)
        {
            if (!allowRecord) return;

            // var team = ship.ShipStatus.Team;
            var playerName = ship.ShipStatus.Player.Name;
            /*if (!EnsureDictionaryEntriesExist(team, playerName)) return;

            TeamStats[team].CrystalsCollected++;
            PlayerStats[playerName].CrystalsCollected++;*/

            if (!_miniGameData.TryGetRoundStats(playerName, out IRoundStats stats))
                return;
            stats.CrystalsCollected++;
            
            switch (crystalProperties.Element)
            {
                case Element.Omni:
                    UpdateStatForPlayer(playerName, stats =>
                    {
                        stats.OmniCrystalsCollected++;
                    });
                    break;

                case Element.Charge:
                    UpdateStatForPlayer(playerName, stats =>
                    {
                        stats.ElementalCrystalsCollected++;
                        stats.ChargeCrystalValue += crystalProperties.crystalValue;
                    });
                    break;

                case Element.Mass:
                    UpdateStatForPlayer(playerName, stats =>
                    {
                        stats.ElementalCrystalsCollected++;
                        stats.MassCrystalValue += crystalProperties.crystalValue;
                    });
                    break;

                case Element.Space:
                    UpdateStatForPlayer(playerName, stats =>
                    {
                        stats.ElementalCrystalsCollected++;
                        stats.SpaceCrystalValue += crystalProperties.crystalValue;
                    });
                    break;

                case Element.Time:
                    UpdateStatForPlayer(playerName, stats =>
                    {
                        stats.ElementalCrystalsCollected++;
                        stats.TimeCrystalValue += crystalProperties.crystalValue;
                    });
                    break;
            }
        }

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

        public void ExecuteSkimmerShipCollision(IShip skimmingShip, IShip ship)
        {
            if (!allowRecord) return;

            // var team = skimmingShip.ShipStatus.Team;
            var playerName = skimmingShip.ShipStatus.Player.Name;

            if (!_miniGameData.TryGetRoundStats(playerName, out var roundStats))
                return;
            roundStats.SkimmerShipCollisions++;
            
            /*if (!EnsureDictionaryEntriesExist(team, playerName)) return;

            TeamStats[team].SkimmerShipCollisions++;
            PlayerStats[playerName].SkimmerShipCollisions++;*/
        }

        void BlockCreated(string creatingPlayerName, TrailBlockProperties createdTrailBlockProperties)
        {
            if (!allowRecord) return;

            if (!_miniGameData.TryGetRoundStats(creatingPlayerName, out var roundStats))
                return;

            roundStats.BlocksCreated++;
            roundStats.BlocksRemaining++;
            roundStats.VolumeCreated += createdTrailBlockProperties.volume;
            roundStats.VolumeRemaining += createdTrailBlockProperties.volume;
            
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

        void BlockDestroyed(string destroyingPlayerName, TrailBlockProperties destroyedTrailBlockProperties)
        {
            if (!allowRecord) return;

            // var victimTeam = destroyedTrailBlockProperties.trailBlock.Team;
            var victimPlayerName = destroyedTrailBlockProperties.trailBlock.PlayerName;

            // Ensure both attacker and victim have stats entries
            /*if (!EnsureDictionaryEntriesExist(destroyingTeam, destroyingPlayerName)) return;
            if (!EnsureDictionaryEntriesExist(victimTeam, victimPlayerName)) return;*/

            // Attacker team stats
            if (!_miniGameData.TryGetRoundStats(destroyingPlayerName, out IRoundStats attackerPlayerStats))
                return; // TeamStats[destroyingTeam];
            
            if (!_miniGameData.TryGetRoundStats(victimPlayerName, out IRoundStats victimPlayerStats))
                return;// TeamStats[victimTeam];
            
            attackerPlayerStats.BlocksDestroyed++;
            if (destroyingPlayerName == victimPlayerName)
                attackerPlayerStats.FriendlyBlocksDestroyed++;
            else
                attackerPlayerStats.HostileBlocksDestroyed++;
            attackerPlayerStats.VolumeDestroyed += destroyedTrailBlockProperties.volume;
            if (destroyingPlayerName == victimPlayerName)
                attackerPlayerStats.FriendlyVolumeDestroyed += destroyedTrailBlockProperties.volume;
            else
                attackerPlayerStats.HostileVolumeDestroyed += destroyedTrailBlockProperties.volume;

            // Victim team remaining stats
            victimPlayerStats.BlocksRemaining--;
            victimPlayerStats.VolumeRemaining -= destroyedTrailBlockProperties.volume;

            // Attacker player stats
            attackerPlayerStats.BlocksDestroyed++;
            if (destroyingPlayerName == victimPlayerName)
                attackerPlayerStats.FriendlyBlocksDestroyed++;
            else
                attackerPlayerStats.HostileBlocksDestroyed++;
            
            attackerPlayerStats.VolumeDestroyed += destroyedTrailBlockProperties.volume;
            if (destroyingPlayerName == victimPlayerName)
                attackerPlayerStats.FriendlyVolumeDestroyed += destroyedTrailBlockProperties.volume;
            else
                attackerPlayerStats.HostileVolumeDestroyed += destroyedTrailBlockProperties.volume;

            // Victim player remaining stats
            /*victimTeamStats.BlocksRemaining--;
            victimTeamStats.VolumeRemaining -= destroyedTrailBlockProperties.volume;*/
        }

        void BlockRestored(string restoringPlayerName, TrailBlockProperties restoredTrailBlockProperties)
        {
            if (!allowRecord) return;

            if (!_miniGameData.TryGetRoundStats(restoringPlayerName, out IRoundStats roundStats))
                return;
            
            /*var ownerPlayerName = restoredTrailBlockProperties.trailBlock.PlayerName;

            if (!EnsureDictionaryEntriesExist(restoringTeam, restoringPlayerName)) return;
            if (!EnsureDictionaryEntriesExist(ownerTeam, ownerPlayerName)) return;*/

            // var teamStats = TeamStats[restoringTeam];
            roundStats.BlocksRestored++;
            roundStats.BlocksRemaining++;
            roundStats.VolumeRestored += restoredTrailBlockProperties.volume;
            roundStats.VolumeRemaining += restoredTrailBlockProperties.volume;

            // var playerStats = PlayerStats[restoringPlayerName];
            /*roundStats.BlocksRestored++;
            roundStats.BlocksRemaining++;
            roundStats.VolumeRestored += restoredTrailBlockProperties.volume;
            roundStats.VolumeRemaining += restoredTrailBlockProperties.volume;*/
        }

        public void BlockVolumeModified(float volume, TrailBlockProperties modifiedTrailBlockProperties)
        {
            if (!allowRecord) return;

            // var ownerTeam = modifiedTrailBlockProperties.trailBlock.Team;
            var ownerPlayerName = modifiedTrailBlockProperties.trailBlock.PlayerName;
            
            if (!_miniGameData.TryGetRoundStats(ownerPlayerName, out IRoundStats roundStats))
                return;

            // if (!EnsureDictionaryEntriesExist(ownerTeam, ownerPlayerName)) return;

            roundStats.VolumeRemaining += volume;
            roundStats.VolumeRemaining += volume;
        }

        public void BlockStolen(Teams stealingTeam, string stealingPlayerName, TrailBlockProperties stolenTrailBlockProperties)
        {
            if (!allowRecord) return;

            // var victimTeam = stolenTrailBlockProperties.trailBlock.Team;

            /*if (!EnsureDictionaryEntriesExist(stealingTeam, stealingPlayerName)) return;
            if (!EnsureDictionaryEntriesExist(victimTeam, victimPlayerName)) return;

            // Stealing team stats
            var stealingTeamStats = TeamStats[stealingTeam];*/

            if (!_miniGameData.TryGetRoundStats(stealingPlayerName, out IRoundStats stealingPlayerStats))
                return;
            
            stealingPlayerStats.BlocksStolen++;
            stealingPlayerStats.BlocksRemaining++;
            stealingPlayerStats.VolumeStolen += stolenTrailBlockProperties.volume;
            stealingPlayerStats.VolumeRemaining += stolenTrailBlockProperties.volume;

            // Stealing player stats
            // var stealingPlayerStats = PlayerStats[stealingPlayerName];
            
            /*stealingPlayerStats.BlocksStolen++;
            stealingPlayerStats.BlocksRemaining++;
            stealingPlayerStats.VolumeStolen += stolenTrailBlockProperties.volume;
            stealingPlayerStats.VolumeRemaining += stolenTrailBlockProperties.volume;*/

            var victimPlayerName = stolenTrailBlockProperties.trailBlock.PlayerName;
            if (!_miniGameData.TryGetRoundStats(victimPlayerName, out IRoundStats victimPlayerStats))
                return;
            
            // Victim team remaining
            // var victimTeamStats = TeamStats[victimTeam];
            victimPlayerStats.BlocksRemaining--;
            victimPlayerStats.VolumeRemaining -= stolenTrailBlockProperties.volume;

            // Victim player remaining
            // var victimPlayerStats = PlayerStats[victimPlayerName];
            /*victimPlayerStats.BlocksRemaining--;
            victimPlayerStats.VolumeRemaining -= stolenTrailBlockProperties.volume;*/
        }

        public void AbilityActivated(Teams team, string playerName, InputEvents abilityType, float duration)
        {
            if (!allowRecord) return;
            /*if (!EnsureDictionaryEntriesExist(team, playerName)) return;

            var teamStats = TeamStats[team];
            var playerStats = PlayerStats[playerName];*/
            
            if (!_miniGameData.TryGetRoundStats(playerName, out IRoundStats playerStats))
                return;

            switch (abilityType)
            {
                case InputEvents.FullSpeedStraightAction:
                    // teamStats.FullSpeedStraightAbilityActiveTime += duration;
                    playerStats.FullSpeedStraightAbilityActiveTime += duration;
                    break;

                case InputEvents.RightStickAction:
                    // teamStats.RightStickAbilityActiveTime += duration;
                    playerStats.RightStickAbilityActiveTime += duration;
                    break;

                case InputEvents.LeftStickAction:
                    // teamStats.LeftStickAbilityActiveTime += duration;
                    playerStats.LeftStickAbilityActiveTime += duration;
                    break;

                case InputEvents.FlipAction:
                    // teamStats.FlipAbilityActiveTime += duration;
                    playerStats.FlipAbilityActiveTime += duration;
                    break;

                case InputEvents.Button1Action:
                    // teamStats.Button1AbilityActiveTime += duration;
                    playerStats.Button1AbilityActiveTime += duration;
                    break;

                case InputEvents.Button2Action:
                    // teamStats.Button2AbilityActiveTime += duration;
                    playerStats.Button2AbilityActiveTime += duration;
                    break;

                case InputEvents.Button3Action:
                    // teamStats.Button3AbilityActiveTime += duration;
                    playerStats.Button3AbilityActiveTime += duration;
                    break;
            }
        }

        protected void OnBlockCreated(TrailBlockEventData data)
        {
            BlockCreated(data.PlayerName, data.TrailBlockProperties);
        }

        protected void OnBlockDestroyed(TrailBlockEventData data)
        {
            BlockDestroyed(data.PlayerName, data.TrailBlockProperties);
        }

        protected void OnBlockRestored(TrailBlockEventData data)
        {
            BlockRestored(data.PlayerName, data.TrailBlockProperties);
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

        public float GetTotalVolume() => _miniGameData.RoundStatsList.Sum(stats => stats.VolumeRemaining);

        public Vector4 GetTeamVolumes()
        {
            var greenVolume = _miniGameData.TryGetRoundStats(Teams.Jade, out var gStats) ? gStats.VolumeRemaining : 0f;
            var redVolume = _miniGameData.TryGetRoundStats(Teams.Ruby, out var rStats) ? rStats.VolumeRemaining : 0f;
            var blueVolume = _miniGameData.TryGetRoundStats(Teams.Blue, out var bStats) ? bStats.VolumeRemaining : 0f;
            var goldVolume = _miniGameData.TryGetRoundStats(Teams.Gold, out var yStats) ? yStats.VolumeRemaining : 0f;
            return new Vector4(greenVolume, redVolume, blueVolume, goldVolume);
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
