using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game;
using CosmicShore.SOAP;
using CosmicShore.Utilities;


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
        GameDataSO gameData;

        protected bool allowRecord = true;
        
        public void LifeformCreated(int cellID)
        {
            if (!allowRecord) return;

            var cellStatsList = gameData.CellStatsList;
            
            if (!cellStatsList.ContainsKey(cellID))
                cellStatsList[cellID] = new CellStats();

            var cs = cellStatsList[cellID];
            cs.LifeFormsInCell++;
            cellStatsList[cellID] = cs;
        }

        public void LifeformDestroyed(int cellID)
        {
            if (!allowRecord) return;

            var cellStatsList = gameData.CellStatsList;
            
            if (!cellStatsList.ContainsKey(cellID))
                cellStatsList[cellID] = new CellStats();

            var cs = cellStatsList[cellID];
            cs.LifeFormsInCell--;
            cellStatsList[cellID] = cs;
        }
        
        public void CrystalCollected(CrystalStats crystalStats)
        {
            if (!allowRecord) return;
            
            var playerName = crystalStats.PlayerName;
            if (!gameData.TryGetRoundStats(playerName, out IRoundStats stats))
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

        public void ExecuteSkimmerShipCollision(string skimmerPlayerName)
        {
            if (!allowRecord) return;
            
            if (!gameData.TryGetRoundStats(skimmerPlayerName, out var roundStats))
                return;
            roundStats.SkimmerShipCollisions++;
        }

        public void PrismCreated(PrismStats prismStats)
        {
            if (!allowRecord) return;

            if (!gameData.TryGetRoundStats(prismStats.PlayerName, out var roundStats))
                return;

            roundStats.BlocksCreated++;
            roundStats.PrismsRemaining++;
            roundStats.VolumeCreated += prismStats.Volume;
            roundStats.VolumeRemaining += prismStats.Volume; 
        }

        public void PrismDestroyed(PrismStats prismStats)
        {
            if (!allowRecord) return;

            var victimPlayerName = prismStats.OtherPlayerName;
            var attackingPlayerName = prismStats.PlayerName;
            
            // Attacker team stats
            if (!gameData.TryGetRoundStats(attackingPlayerName, out IRoundStats attackerPlayerStats))
                return;
            
            if (!gameData.TryGetRoundStats(victimPlayerName, out IRoundStats victimPlayerStats))
                return;
            
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
        }

        public void PrismRestored(PrismStats prismStats)
        {
            if (!allowRecord) return;

            var restoringPlayerName = prismStats.PlayerName;

            if (!gameData.TryGetRoundStats(restoringPlayerName, out IRoundStats roundStats))
                return;
            
            roundStats.BlocksRestored++;
            roundStats.PrismsRemaining++;
            roundStats.VolumeRestored += prismStats.Volume;
            roundStats.VolumeRemaining += prismStats.Volume;
        }

        public void PrismVolumeModified(PrismStats prismStats)
        {
            if (!allowRecord) return;

            var ownerPlayerName = prismStats.PlayerName;
            
            if (!gameData.TryGetRoundStats(ownerPlayerName, out IRoundStats roundStats))
                return;

            roundStats.VolumeCreated += prismStats.Volume;
            roundStats.VolumeRemaining += prismStats.Volume;
        }

        public void PrismStolen(PrismStats prismStats)
        {
            if (!allowRecord) return;

            var stealingPlayerName = prismStats.PlayerName;
            if (!gameData.TryGetRoundStats(stealingPlayerName, out IRoundStats stealingPlayerStats))
                return;
            
            stealingPlayerStats.PrismStolen++;
            stealingPlayerStats.PrismsRemaining++;
            stealingPlayerStats.VolumeStolen += prismStats.Volume;
            stealingPlayerStats.VolumeRemaining += prismStats.Volume;

            var victimPlayerName = prismStats.OtherPlayerName;
            if (!gameData.TryGetRoundStats(victimPlayerName, out IRoundStats victimPlayerStats))
                return;
            
            victimPlayerStats.PrismsRemaining--;
            victimPlayerStats.VolumeRemaining -= prismStats.Volume;
        }

        public void RegisterAbilityExecuted(AbilityStats abilityStats)
        {
            if (!allowRecord) return;
            
            if (!gameData.TryGetRoundStats(abilityStats.PlayerName, out IRoundStats playerStats))
                return;

            switch (abilityStats.ControlType)
            {
                case InputEvents.FullSpeedStraightAction:
                    playerStats.FullSpeedStraightAbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.RightStickAction:
                    playerStats.RightStickAbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.LeftStickAction:
                    playerStats.LeftStickAbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.FlipAction:
                    playerStats.FlipAbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.Button1Action:
                    playerStats.Button1AbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.Button2Action:
                    playerStats.Button2AbilityActiveTime += abilityStats.Duration;
                    break;

                case InputEvents.Button3Action:
                    playerStats.Button3AbilityActiveTime += abilityStats.Duration;
                    break;
            }
        }
        void UpdateStatForPlayer(string playerName, Action<IRoundStats> updateAction)
        {
            if (!gameData.TryGetRoundStats(playerName, out var roundStats))
                return;

            updateAction(roundStats);
        }
    }
}
