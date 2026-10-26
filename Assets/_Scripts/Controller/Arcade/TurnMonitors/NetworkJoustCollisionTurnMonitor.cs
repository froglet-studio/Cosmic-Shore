// NetworkJoustCollisionTurnMonitor.cs
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Unity.Netcode;
using System.Linq;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The joust collision turn monitor. Resolves the joust target from the End Game Conditions tool, owns the
    /// collision sync RPCs so that no direct reference to any game controller is needed, detects collisions
    /// locally, syncs them across the network, and ends the turn when any player reaches
    /// <see cref="CollisionsNeeded"/>. It used to sit on a local-only base, <c>JoustCollisionTurnMonitor</c>,
    /// which no scene placed; that base was folded in here on 2026-10-25 (Docs/REFACTOR_BACKLOG.md M7), its
    /// steps kept in their order.
    /// </summary>
    public class NetworkJoustCollisionTurnMonitor : TurnMonitor
    {
        // RESOLVED joust target - set in StartMonitor from EndConditionOverridesSO
        // (FrogletTools > Game Modes > End Game Conditions)→ default 3. Intentionally NOT a
        // [SerializeField]: end-game counts are authored only via the tool, never per-scene.
        // Do not re-add [SerializeField] here (see /EndGameConditions skill).
        int collisionsNeeded;
        public int CollisionsNeeded => collisionsNeeded;

        IRoundStats ownStats;

        // Stats this monitor actually subscribed to. Unsubscription must run off THIS
        // list, never gameData.RoundStatsList: on a mid-turn scene exit, SceneLoader's
        // ResetRuntimeData clears the roster BEFORE the old scene's objects are
        // destroyed, so a list-based unsubscribe loop detaches nothing and the handlers
        // leak onto the persistent human RoundStats (Docs/ScoringSystem/BUGS.md B15).
        readonly List<IRoundStats> _subscribedStats = new();

        public override void StartMonitor()
        {
            InitializeOwnStats();

            // End-game count comes from the tool: FrogletTools > Game Modes > End Game Conditions
            // (Resources/EndConditionOverrides). 0 there = default (3). See /EndGameConditions skill.
            var overrides = EndConditionOverridesSO.Instance;
            collisionsNeeded = overrides != null ? overrides.GetJoustCount() : EndConditionOverridesSO.DefaultJoustCount;

            // Publish the joust target so the controller (and Phase B: scoreboard) can read
            // it from one place - mirrors SkimRace's CrystalTargetCount. A scene constant, so
            // every peer writes the same value (R10).
            gameData.JoustTargetCount = collisionsNeeded;

            if (ownStats != null)
            {
                // Idempotent: StartMonitor can be invoked more than once per turn
                // (e.g. duplicated SOAP subscriptions) - never double-attach.
                ownStats.OnJoustCollisionChanged -= OnJoustCollisionChanged;
                ownStats.OnJoustCollisionChanged += OnJoustCollisionChanged;
                UpdateUI();
            }

            base.StartMonitor();

            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[NetworkJoustMonitor] StartMonitor - IsServer={IsServer}, " +
                $"CollisionsNeeded={CollisionsNeeded}, " +
                $"Players={gameData.RoundStatsList.Count}, " +
                $"Names=[{string.Join(", ", gameData.RoundStatsList.Select(s => s.Name))}]");

            // ALL machines subscribe - client needs to report its own collisions up to server,
            // and the HUD's "jousts remaining" readout needs to reflect the local player's
            // DOMAIN aggregate, which changes whenever ANY teammate jousts.
            foreach (var stat in gameData.RoundStatsList)
            {
                if (stat == null || _subscribedStats.Contains(stat)) continue;
                stat.OnJoustCollisionChanged += OnCollisionChanged;
                stat.OnJoustCollisionChanged += OnAnyJoustChangedUI;
                _subscribedStats.Add(stat);
            }

            UpdateDomainRemainingUI();
        }

        public override void StopMonitor()
        {
            foreach (var stat in _subscribedStats)
            {
                if (stat == null) continue;
                stat.OnJoustCollisionChanged -= OnCollisionChanged;
                stat.OnJoustCollisionChanged -= OnAnyJoustChangedUI;
            }
            _subscribedStats.Clear();

            base.StopMonitor();

            if (ownStats != null)
                ownStats.OnJoustCollisionChanged -= OnJoustCollisionChanged;
        }

        public override void OnDestroy()
        {
            // Safety net for destruction paths that bypass StopMonitor - detaching
            // from the persistent RoundStats must never depend on the turn ending.
            StopMonitor();
            base.OnDestroy();
        }

        void OnJoustCollisionChanged(IRoundStats stats) => UpdateUI();

        // The local player's own remaining count; the domain readout below follows it.
        void UpdateUI()
        {
            InitializeOwnStats();
            if (ownStats == null) return;

            int remaining = Mathf.Max(0, collisionsNeeded - ownStats.JoustCollisions);
            onUpdateTurnMonitorDisplay?.Raise(remaining.ToString());
        }

        void InitializeOwnStats()
        {
            if (ownStats != null) return;

            if (!gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats))
                CSDebug.LogWarning("[NetworkJoustCollisionTurnMonitor] No round stats found for local player");
        }

        void OnAnyJoustChangedUI(IRoundStats _) => UpdateDomainRemainingUI();

        void UpdateDomainRemainingUI()
        {
            if (gameData.LocalPlayer == null) return;
            // Remaining = local player's DOMAIN joust deficit (the rule owns target + sum).
            int remaining = gameData.ScoringRule.Remaining(gameData, gameData.LocalPlayer.Domain);
            if (onUpdateTurnMonitorDisplay)
                onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }

        void OnCollisionChanged(IRoundStats stats)
        {
            if (IsServer)
            {
                // Server already has the correct local value from the setter -
                // just broadcast to clients. Do NOT re-assign JoustCollisions here
                // or it will re-trigger this handler and cause infinite recursion.
                SyncCollision_ClientRpc(stats.Name, stats.JoustCollisions);
            }
            else
            {
                // Client detected a collision the server missed (high-speed physics)
                // - report it up so the server can authoritatively sync everyone
                ReportCollision_ServerRpc(stats.Name, stats.JoustCollisions);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportCollision_ServerRpc(string playerName, int collisionCount)
        {
            var stats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
            if (stats == null)
            {
                CSDebug.LogError($"[NetworkJoustMonitor] ServerRpc: no stats for '{playerName}'");
                return;
            }

            // Only accept if the client reports a higher count (prevent stale/duplicate reports)
            if (collisionCount <= stats.JoustCollisions) return;

            stats.JoustCollisions = collisionCount;
            SyncCollision_ClientRpc(playerName, collisionCount);
        }

        [ClientRpc]
        void SyncCollision_ClientRpc(string playerName, int collisionCount)
        {
            // Server already has the correct value - only clients need the update.
            if (IsServer) return;
            if (!gameData.TryGetRoundStats(playerName, out IRoundStats stats)) return;
            stats.JoustCollisions = collisionCount;
        }

        public override bool CheckForEndOfTurn()
        {
            // Only server ends the turn authoritatively
            if (!IsServer) return false;

            // End condition delegated to the mode's ScoringRule: an active domain's summed
            // jousts reaching the target. Domain teammates finish the objective together.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }
    }
}
