using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;


namespace CosmicShore.Gameplay
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
        public string OwnName;
        public float Volume;
        public string AttackerName;
    }

    [Serializable]
    public struct AbilityStats
    {
        public string PlayerName;
        public InputEvents ControlType;
        public float Duration;
    }

    public class StatsManager : MonoBehaviour
    {
        [SerializeField]
        GameDataSO gameData;

        [SerializeField]
        CellRuntimeDataSO cellData;

        [SerializeField]
        NetcodeHooks _netcodeHooks;

        /// <summary>
        /// True when this machine is allowed to mutate stats. In multiplayer, only the
        /// server records — clients receive replicated values via NetworkVariable.
        /// In singleplayer (no NetworkManager listening), every machine records its own.
        ///
        /// We query <see cref="Unity.Netcode.NetworkManager"/> directly instead of relying
        /// on a cached flag set by <see cref="OnNetworkSpawn"/>: <see cref="StatsManager"/>
        /// lives in the Bootstrap scene (DontDestroyOnLoad), and its <see cref="NetcodeHooks"/>
        /// NetworkObject is not part of any network-loaded scene, so its OnNetworkSpawn
        /// callback is unreliable. Without this fix, the cached flag stayed at the default
        /// <c>true</c> on clients and the client locally double-incremented every crystal
        /// collection — manifesting as "client shows 8, host shows 0" desync.
        /// </summary>
        bool AllowRecord
        {
            get
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm == null || !nm.IsListening) return true; // singleplayer or no network yet
                return nm.IsServer;                              // multiplayer: server only
            }
        }

        void OnEnable()
        {
            if (_netcodeHooks != null)
                _netcodeHooks.OnNetworkSpawnHook += OnNetworkSpawn;
        }

        void OnDisable()
        {
            if (_netcodeHooks != null)
                _netcodeHooks.OnNetworkSpawnHook -= OnNetworkSpawn;
        }

        void OnNetworkSpawn()
        {
            // No-op — kept for backward compatibility with existing inspector wiring.
            // Authoritative gating now lives in the AllowRecord property.
        }

        public void LifeformCreated(int cellID)
        {
            if (!AllowRecord || cellData == null) return;

            var cellStatsList = cellData.CellStatsList;

            if (!cellStatsList.ContainsKey(cellID))
                cellStatsList[cellID] = new CellStats();

            var cs = cellStatsList[cellID];
            cs.LifeFormsInCell++;
            cellStatsList[cellID] = cs;
        }

        public void LifeformDestroyed(int cellID)
        {
            if (!AllowRecord || cellData == null) return;

            var cellStatsList = cellData.CellStatsList;

            if (!cellStatsList.ContainsKey(cellID))
                cellStatsList[cellID] = new CellStats();

            var cs = cellStatsList[cellID];
            cs.LifeFormsInCell--;
            cellStatsList[cellID] = cs;
        }

        public void CrystalCollected(CrystalStats crystalStats)
        {
            if (!AllowRecord) return;

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
            if (!AllowRecord) return;

            if (!gameData.TryGetRoundStats(skimmerPlayerName, out var roundStats))
                return;
            roundStats.SkimmerShipCollisions++;
        }

        public void ExecuteJoustCollision(string joustPlayerName)
        {
            if (!AllowRecord) return;

            if (!gameData.TryGetRoundStats(joustPlayerName, out var roundStats))
            {
                CSDebug.LogWarning($"[StatsManager] ExecuteJoustCollision: no RoundStats for '{joustPlayerName}'. " +
                    $"Available: [{string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}]");
                return;
            }

            roundStats.JoustCollisions++;
            CSDebug.Log($"[StatsManager] JoustCollision recorded for '{joustPlayerName}': {roundStats.JoustCollisions}");
        }

        public void PrismCreated(PrismStats prismStats)
        {
            if (!AllowRecord) return;

            if (!gameData.TryGetRoundStats(prismStats.OwnName, out var roundStats))
                return;

            roundStats.BlocksCreated++;
            roundStats.PrismsRemaining++;
            roundStats.VolumeCreated += prismStats.Volume;
            roundStats.VolumeRemaining += prismStats.Volume;
        }

        public void PrismDestroyed(PrismStats prismStats)
        {
            if (!AllowRecord) return;

            var attackingPlayerName = prismStats.AttackerName;
            var victimPlayerName = prismStats.OwnName;

            var hasAttacker = gameData.TryGetRoundStats(attackingPlayerName, out IRoundStats attackerPlayerStats);
            var hasVictim = gameData.TryGetRoundStats(victimPlayerName, out IRoundStats victimPlayerStats);

            if (hasAttacker)
            {
                attackerPlayerStats.BlocksDestroyed++;
                attackerPlayerStats.TotalVolumeDestroyed += prismStats.Volume;

                var isFriendly =
                    attackingPlayerName == victimPlayerName ||
                    (hasVictim && attackerPlayerStats.Domain == victimPlayerStats.Domain);

                if (isFriendly)
                {
                    attackerPlayerStats.FriendlyPrismsDestroyed++;
                    attackerPlayerStats.FriendlyVolumeDestroyed += prismStats.Volume;
                }
                else
                {
                    attackerPlayerStats.HostilePrismsDestroyed++;
                    attackerPlayerStats.HostileVolumeDestroyed += prismStats.Volume;
                }
            }

            if (!hasVictim) return;
            victimPlayerStats.PrismsRemaining--;
            victimPlayerStats.VolumeRemaining -= prismStats.Volume;
        }

        public void PrismRestored(PrismStats prismStats)
        {
            if (!AllowRecord) return;

            var restoringPlayerName = prismStats.OwnName;

            if (!gameData.TryGetRoundStats(restoringPlayerName, out IRoundStats roundStats))
                return;

            roundStats.BlocksRestored++;
            roundStats.PrismsRemaining++;
            roundStats.VolumeRestored += prismStats.Volume;
            roundStats.VolumeRemaining += prismStats.Volume;
        }

        public void PrismVolumeModified(PrismStats prismStats)
        {
            if (!AllowRecord) return;

            var ownerPlayerName = prismStats.OwnName;

            if (!gameData.TryGetRoundStats(ownerPlayerName, out IRoundStats roundStats))
                return;

            roundStats.VolumeCreated += prismStats.Volume;
            roundStats.VolumeRemaining += prismStats.Volume;
        }

        public void PrismStolen(PrismStats prismStats)
        {
            if (!AllowRecord) return;

            var stealingPlayerName = prismStats.OwnName;
            if (!gameData.TryGetRoundStats(stealingPlayerName, out IRoundStats stealingPlayerStats))
                return;

            stealingPlayerStats.PrismStolen++;
            stealingPlayerStats.PrismsRemaining++;
            stealingPlayerStats.VolumeStolen += prismStats.Volume;
            stealingPlayerStats.VolumeRemaining += prismStats.Volume;

            var victimPlayerName = prismStats.AttackerName;
            if (!gameData.TryGetRoundStats(victimPlayerName, out IRoundStats victimPlayerStats))
                return;

            victimPlayerStats.PrismsRemaining--;
            victimPlayerStats.VolumeRemaining -= prismStats.Volume;
        }

        public void RegisterAbilityExecuted(AbilityStats abilityStats)
        {
            if (!AllowRecord) return;

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
