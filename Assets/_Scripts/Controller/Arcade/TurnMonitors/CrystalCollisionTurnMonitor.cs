using System; // Required for Action
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public class CrystalCollisionTurnMonitor : TurnMonitor
    {
        // The RESOLVED crystal target for this turn - set in StartMonitor from
        // EndConditionOverridesSO (FrogletTools > Game Modes > End Game Conditions)→ waypoints → 39.
        // Intentionally NOT a [SerializeField]: end-game counts are authored only via the tool,
        // never per-scene. Do not re-add [SerializeField] here (see /EndGameConditions skill).
        protected int CrystalCollisions;
        protected IRoundStats ownStats;

        [Header("Optional Configuration")]
        [SerializeField] SpawnableWaypointTrack optionalEnvironment;

        [Tooltip("Laps per intensity level (index 0 = intensity 1), matching the track's waypoint " +
                 "list by index. Lets a long track run fewer laps than a short one. An entry <= 0, " +
                 "or a missing entry for the current intensity, falls back to Optional Laps.")]
        [SerializeField] List<int> lapsPerIntensity = new();

        [Tooltip("Fallback lap count, used for any intensity Laps Per Intensity does not cover.")]
        [SerializeField] int optionalLaps = 4;

        public override void StartMonitor()
        {
            CrystalCollisions = GetCrystalCollisionCount();

            InitializeOwnStats();
            if (ownStats != null)
            {
                // Idempotent: StartMonitor can be invoked more than once per turn
                // (e.g. duplicated SOAP subscriptions) - never double-attach.
                ownStats.OnCrystalsCollectedChanged -= UpdateCrystals;
                ownStats.OnCrystalsCollectedChanged += UpdateCrystals;
            }
            UpdateCrystals(ownStats);
            base.StartMonitor();
        }

        public override void StopMonitor()
        {
            base.StopMonitor();
            if (ownStats != null) ownStats.OnCrystalsCollectedChanged -= UpdateCrystals;
        }
        
        public override bool CheckForEndOfTurn()
        {
            if (ownStats == null) return false;
            return ownStats.CrystalsCollected >= CrystalCollisions;
        }
        
        protected virtual void UpdateCrystals(IRoundStats stats) => UpdateCrystalsRemainingUI();

        protected virtual void UpdateCrystalsRemainingUI()
        {
            string message = GetRemainingCrystalsCountToCollect();
            if (onUpdateTurnMonitorDisplay) onUpdateTurnMonitorDisplay.Raise(message);
        }
        
        public string GetRemainingCrystalsCountToCollect()
        {
            InitializeOwnStats();
            if (ownStats == null) return CrystalCollisions.ToString();
            int remaining = CrystalCollisions - ownStats.CrystalsCollected;
            return Mathf.Max(0, remaining).ToString();
        }

        protected virtual void InitializeOwnStats()
        {
            if (ownStats != null) return;
            if (gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats)) return;
        }

        protected int GetCrystalCollisionCount()
        {
            int autoCalc = ComputeAutoCalcCount();

            // End-game counts are the tool's authority: FrogletTools > Game Modes > End Game Conditions
            // (Resources/EndConditionOverrides). A 0 there means "auto" → this autoCalc.
            // Keyed by GameMode so SkimRace and Crystal Capture (same monitor class) stay independent.
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null ? overrides.GetCrystalCount(gameData.GameMode, autoCalc) : autoCalc;
        }

        int ComputeAutoCalcCount()
        {
            if (optionalEnvironment)
            {
                int intensity = optionalEnvironment.intensityLevel;
                int waypointCount = optionalEnvironment.waypoints[intensity - 1].positions.Count;
                return waypointCount * ResolveLaps(intensity);
            }

            CSDebug.LogWarning($"[CrystalCollisionTurnMonitor] No crystal count configured for {gameObject.name} and no waypoints. Defaulting to 39.");
            return 39;
        }

        /// <summary>
        /// Laps for the given 1-based intensity: the per-intensity entry when one is
        /// authored and positive, otherwise the flat <see cref="optionalLaps"/>. Keeps
        /// scenes that predate <see cref="lapsPerIntensity"/> (empty list) on their
        /// original single-value behavior.
        /// </summary>
        int ResolveLaps(int intensity)
        {
            int index = intensity - 1;
            if (lapsPerIntensity != null && index >= 0 && index < lapsPerIntensity.Count && lapsPerIntensity[index] > 0)
                return lapsPerIntensity[index];

            return Mathf.Max(1, optionalLaps);
        }
    }
}