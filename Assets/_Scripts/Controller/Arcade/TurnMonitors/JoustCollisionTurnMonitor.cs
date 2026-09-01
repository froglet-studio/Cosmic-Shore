using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public class JoustCollisionTurnMonitor : TurnMonitor
    {
        // RESOLVED joust target - set in StartMonitor from EndConditionOverridesSO
        // (FrogletTools > Game Modes > End Game Conditions)→ default 3. Intentionally NOT a
        // [SerializeField]: end-game counts are authored only via the tool, never per-scene.
        // Do not re-add [SerializeField] here (see /EndGameConditions skill).
        int collisionsNeeded;
        public int CollisionsNeeded => collisionsNeeded;

        IRoundStats ownStats;

        public override void StartMonitor()
        {
            InitializeOwnStats();

            // End-game count comes from the tool: FrogletTools > Game Modes > End Game Conditions
            // (Resources/EndConditionOverrides). 0 there = default (3). See /EndGameConditions skill.
            var overrides = EndConditionOverridesSO.Instance;
            collisionsNeeded = overrides != null ? overrides.GetJoustCount() : EndConditionOverridesSO.DefaultJoustCount;

            // Publish the joust target so the controller (and Phase B: scoreboard) can read
            // it from one place - mirrors HexRace's CrystalTargetCount. A scene constant, so
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
        }

        public override void StopMonitor()
        {
            base.StopMonitor();

            if (ownStats != null)
                ownStats.OnJoustCollisionChanged -= OnJoustCollisionChanged;
        }
        
        public override bool CheckForEndOfTurn()
        {
            InitializeOwnStats();
            return ownStats != null && ownStats.JoustCollisions >= collisionsNeeded;
        }

        void OnJoustCollisionChanged(IRoundStats stats) => UpdateUI();

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
                CSDebug.LogWarning("[JoustCollisionTurnMonitor] No round stats found for local player");
        }
    }
}